using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Editor.EffectGraph
{
    public sealed class EffectGraphContainerModel
    {
        private readonly Func<EffectData[]> _getEffects;
        private readonly Action<EffectData[]> _setEffects;

        internal EffectGraphContainerModel(
            string path,
            string displayName,
            EffectGraphContainerModel parent,
            EffectData ownerEffect,
            FieldInfo ownerField,
            Func<EffectData[]> getEffects,
            Action<EffectData[]> setEffects)
        {
            Path = path ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Parent = parent;
            OwnerEffect = ownerEffect;
            OwnerField = ownerField;
            _getEffects = getEffects ?? throw new ArgumentNullException(nameof(getEffects));
            _setEffects = setEffects ?? throw new ArgumentNullException(nameof(setEffects));
        }

        public string Path { get; }

        public string DisplayName { get; }

        public EffectGraphContainerModel Parent { get; }

        public EffectData OwnerEffect { get; }

        public FieldInfo OwnerField { get; }

        public bool IsRoot => OwnerEffect == null;

        public EffectData[] Effects => _getEffects() ?? Array.Empty<EffectData>();

        internal void SetEffects(EffectData[] effects)
        {
            _setEffects(effects ?? Array.Empty<EffectData>());
        }
    }

    public sealed class EffectGraphModel
    {
        private static readonly Dictionary<Type, FieldInfo[]> s_effectArrayFieldsByType = new();

        private readonly EffectGraphBinding _binding;
        private readonly Dictionary<string, EffectGraphContainerModel> _containersByPath = new(StringComparer.Ordinal);
        private readonly Dictionary<EffectData, EffectGraphContainerModel> _ownerContainers = new();
        private readonly Dictionary<EffectData, Dictionary<FieldInfo, EffectGraphContainerModel>> _nestedContainers = new();
        private readonly List<EffectGraphContainerModel> _containers = new();

        public EffectGraphModel(EffectGraphBinding binding)
        {
            _binding = binding ?? throw new ArgumentNullException(nameof(binding));
            Rebuild();
        }

        public EffectGraphBinding Binding => _binding;

        public EffectGraphContainerModel Root { get; private set; }

        public IReadOnlyList<EffectGraphContainerModel> Containers => _containers;

        public void Rebuild()
        {
            _containersByPath.Clear();
            _ownerContainers.Clear();
            _nestedContainers.Clear();
            _containers.Clear();

            Root = RegisterContainer(new EffectGraphContainerModel(
                "root",
                "Root Effects",
                null,
                null,
                null,
                _binding.GetRootEffects,
                _binding.SetRootEffects));

            BuildNestedContainers(Root, new HashSet<EffectData>());
        }

        public EffectGraphContainerModel FindContainer(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && _containersByPath.TryGetValue(path, out EffectGraphContainerModel container)
                ? container
                : null;
        }

        public EffectGraphContainerModel GetOwnerContainer(EffectData effect)
        {
            return effect != null && _ownerContainers.TryGetValue(effect, out EffectGraphContainerModel container)
                ? container
                : null;
        }

        public EffectGraphContainerModel GetNestedContainer(EffectData ownerEffect, FieldInfo field)
        {
            if (ownerEffect == null || field == null ||
                !_nestedContainers.TryGetValue(ownerEffect, out Dictionary<FieldInfo, EffectGraphContainerModel> fields))
            {
                return null;
            }

            return fields.TryGetValue(field, out EffectGraphContainerModel container) ? container : null;
        }

        public IReadOnlyList<FieldInfo> GetNestedEffectArrayFields(EffectData effect)
        {
            if (effect == null)
                return Array.Empty<FieldInfo>();

            return GetEffectArrayFields(effect.GetType());
        }

        public bool IsNestedEffectArrayField(FieldInfo field)
        {
            return field != null &&
                   field.FieldType.IsArray &&
                   typeof(EffectData).IsAssignableFrom(field.FieldType.GetElementType());
        }

        public EffectData AddEffect(EffectGraphContainerModel target, Type effectType, int insertIndex)
        {
            if (target == null || !EffectGraphTypeRegistry.TryCreate(effectType, out EffectData effect))
                return null;

            List<EffectData> effects = new(target.Effects);
            effects.Insert(Math.Clamp(insertIndex, 0, effects.Count), effect);
            target.SetEffects(effects.ToArray());
            NotifyAndRebuild();
            return effect;
        }

        public bool RemoveEffect(EffectGraphContainerModel source, int sourceIndex)
        {
            if (source == null || sourceIndex < 0 || sourceIndex >= source.Effects.Length)
                return false;

            List<EffectData> effects = new(source.Effects);
            effects.RemoveAt(sourceIndex);
            source.SetEffects(effects.ToArray());
            NotifyAndRebuild();
            return true;
        }

        public bool MoveEffect(
            EffectGraphContainerModel source,
            int sourceIndex,
            EffectGraphContainerModel target,
            int targetIndex)
        {
            if (source == null || target == null || sourceIndex < 0 || sourceIndex >= source.Effects.Length)
                return false;

            EffectData movingEffect = source.Effects[sourceIndex];
            if (movingEffect == null || IsContainerInsideEffectSubtree(target, movingEffect))
                return false;

            List<EffectData> sourceEffects = new(source.Effects);
            List<EffectData> targetEffects = ReferenceEquals(source, target)
                ? sourceEffects
                : new List<EffectData>(target.Effects);

            sourceEffects.RemoveAt(sourceIndex);
            if (ReferenceEquals(source, target) && targetIndex > sourceIndex)
                targetIndex--;

            targetEffects.Insert(Math.Clamp(targetIndex, 0, targetEffects.Count), movingEffect);
            source.SetEffects(sourceEffects.ToArray());
            if (!ReferenceEquals(source, target))
                target.SetEffects(targetEffects.ToArray());

            NotifyAndRebuild();
            return true;
        }

        private EffectGraphContainerModel RegisterContainer(EffectGraphContainerModel container)
        {
            _containersByPath[container.Path] = container;
            _containers.Add(container);
            return container;
        }

        private void BuildNestedContainers(EffectGraphContainerModel container, HashSet<EffectData> ancestors)
        {
            EffectData[] effects = container.Effects;
            for (int effectIndex = 0; effectIndex < effects.Length; effectIndex++)
            {
                EffectData effect = effects[effectIndex];
                if (effect == null)
                    continue;

                _ownerContainers[effect] = container;
                if (!ancestors.Add(effect))
                    continue;

                IReadOnlyList<FieldInfo> fields = GetEffectArrayFields(effect.GetType());
                for (int fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                {
                    FieldInfo field = fields[fieldIndex];
                    string displayName = EditorLabelUtility.GetLabel(field);
                    string path = $"{container.Path}/{effectIndex}/{field.Name}";
                    EffectGraphContainerModel nested = RegisterContainer(new EffectGraphContainerModel(
                        path,
                        displayName,
                        container,
                        effect,
                        field,
                        () => (EffectData[])field.GetValue(effect) ?? Array.Empty<EffectData>(),
                        value => field.SetValue(effect, value ?? Array.Empty<EffectData>())));

                    if (!_nestedContainers.TryGetValue(effect, out Dictionary<FieldInfo, EffectGraphContainerModel> nestedByField))
                    {
                        nestedByField = new Dictionary<FieldInfo, EffectGraphContainerModel>();
                        _nestedContainers.Add(effect, nestedByField);
                    }

                    nestedByField[field] = nested;
                    BuildNestedContainers(nested, ancestors);
                }

                ancestors.Remove(effect);
            }
        }

        private bool IsContainerInsideEffectSubtree(EffectGraphContainerModel target, EffectData effect)
        {
            if (target == null || effect == null)
                return false;

            if (ReferenceEquals(target.OwnerEffect, effect))
                return true;

            return target.OwnerEffect != null && ContainsEffect(effect, target.OwnerEffect, new HashSet<EffectData>());
        }

        private bool ContainsEffect(EffectData root, EffectData candidate, HashSet<EffectData> visited)
        {
            if (root == null || candidate == null || !visited.Add(root))
                return false;

            if (ReferenceEquals(root, candidate))
                return true;

            IReadOnlyList<FieldInfo> fields = GetEffectArrayFields(root.GetType());
            for (int fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
            {
                EffectData[] effects = (EffectData[])fields[fieldIndex].GetValue(root);
                if (effects == null)
                    continue;

                for (int effectIndex = 0; effectIndex < effects.Length; effectIndex++)
                {
                    if (ContainsEffect(effects[effectIndex], candidate, visited))
                        return true;
                }
            }

            return false;
        }

        private void NotifyAndRebuild()
        {
            _binding.NotifyChanged();
            Rebuild();
        }

        private static FieldInfo[] GetEffectArrayFields(Type type)
        {
            if (type == null)
                return Array.Empty<FieldInfo>();

            if (s_effectArrayFieldsByType.TryGetValue(type, out FieldInfo[] fields))
                return fields;

            fields = type
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => field.FieldType.IsArray && typeof(EffectData).IsAssignableFrom(field.FieldType.GetElementType()))
                .OrderBy(field => field.MetadataToken)
                .ToArray();
            s_effectArrayFieldsByType[type] = fields;
            return fields;
        }
    }
}
