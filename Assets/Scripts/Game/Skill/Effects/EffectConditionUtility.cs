using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;

namespace CrystalMagic.Game.Skill.Effects
{
    public static class EffectConditionUtility
    {
        public const string OriginEntityKey = "effect.context.originEntity";
        public const string TargetEntityKey = "effect.context.targetEntity";
        public const string OtherEntityKey = "effect.context.otherEntity";
        public const string PositionKey = "effect.context.position";
        public const string TriggerValueKey = "effect.context.triggerValue";

        private static ComparatorFactory s_comparatorFactory;

        public static bool Pass(IReadOnlyList<ConditionConfig> conditions, SkillContent context, Entity evaluatedEntity)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            if (context == null || evaluatedEntity == Entity.Null)
                return false;

            EntityManager entityManager = context.EntityManager;
            if (!entityManager.Exists(evaluatedEntity))
                return false;

            if (!UnitSourceDispatcherSystem.TryGet(entityManager, out UnitSourceDispatcher dispatcher))
                return false;
            UnitSourceResolver sources = new(evaluatedEntity);
            Entity other = context.HasOtherEntity
                ? context.OtherEntity
                : UnitVariableSource.GetOther(entityManager, evaluatedEntity);
            sources.Update(evaluatedEntity, other, in dispatcher);

            Comparator comparator = GetComparatorFactory().BuildComparator(
                conditions,
                new EffectConditionValueResolver(sources, context));
            return comparator.GetResult(sources);
        }

        private static ComparatorFactory GetComparatorFactory()
        {
            if (s_comparatorFactory != null)
                return s_comparatorFactory;

            s_comparatorFactory = new ComparatorFactory();
            ComparatorRegistry.RegisterAll(s_comparatorFactory);
            return s_comparatorFactory;
        }

        private sealed class EffectConditionValueResolver : IComparatorValueResolver
        {
            private readonly UnitSourceResolver _unitSources;
            private readonly SkillContent _context;

            public EffectConditionValueResolver(UnitSourceResolver unitSources, SkillContent context)
            {
                _unitSources = unitSources;
                _context = context;
            }

            public bool TryGet(string key, out IParameterizedUnitValueGetter getter)
            {
                switch (key)
                {
                    case OriginEntityKey:
                        getter = new ContextValueGetter(_context, EffectContextValueKind.OriginEntity);
                        return true;
                    case TargetEntityKey:
                        getter = new ContextValueGetter(_context, EffectContextValueKind.TargetEntity);
                        return true;
                    case OtherEntityKey:
                        getter = new ContextValueGetter(_context, EffectContextValueKind.OtherEntity);
                        return true;
                    case PositionKey:
                        getter = new ContextValueGetter(_context, EffectContextValueKind.Position);
                        return true;
                    case TriggerValueKey:
                        getter = new ContextValueGetter(_context, EffectContextValueKind.TriggerValue);
                        return true;
                    default:
                        return ((IComparatorValueResolver)_unitSources).TryGet(key, out getter);
                }
            }

        }

        private enum EffectContextValueKind : byte
        {
            OriginEntity,
            TargetEntity,
            OtherEntity,
            Position,
            TriggerValue,
        }

        private sealed class ContextValueGetter : IParameterizedUnitValueGetter
        {
            private static readonly ComparatorParameterDefinition[] s_parameters = System.Array.Empty<ComparatorParameterDefinition>();
            private readonly SkillContent _context;
            private readonly EffectContextValueKind _kind;

            public ContextValueGetter(SkillContent context, EffectContextValueKind kind)
            {
                _context = context;
                _kind = kind;
            }

            public UnitValueCategory ReturnType => _kind switch
            {
                EffectContextValueKind.OriginEntity => UnitValueCategory.Entity,
                EffectContextValueKind.TargetEntity => UnitValueCategory.Entity,
                EffectContextValueKind.OtherEntity => UnitValueCategory.Entity,
                EffectContextValueKind.Position => UnitValueCategory.Float3,
                EffectContextValueKind.TriggerValue => UnitValueCategory.Number,
                _ => UnitValueCategory.None,
            };
            public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;

            public bool TryGet(UnitValue[] parameters, out UnitValue value)
            {
                if (parameters != null && parameters.Length != 0)
                {
                    value = UnitValue.None;
                    return false;
                }

                value = _kind switch
                {
                    EffectContextValueKind.OriginEntity when _context.HasOriginEntity => UnitValue.FromEntity(_context.OriginEntity),
                    EffectContextValueKind.TargetEntity when _context.HasTargetEntity => UnitValue.FromEntity(_context.TargetEntity),
                    EffectContextValueKind.OtherEntity when _context.HasOtherEntity => UnitValue.FromEntity(_context.OtherEntity),
                    EffectContextValueKind.Position when _context.HasPosition => UnitValue.FromFloat3(new float3(
                        _context.Position.x,
                        _context.Position.y,
                        _context.Position.z)),
                    EffectContextValueKind.TriggerValue => UnitValue.FromFloat(_context.TriggerValue),
                    _ => UnitValue.None,
                };
                return value.Category == ReturnType;
            }
        }
    }
}
