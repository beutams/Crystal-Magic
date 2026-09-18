using System;
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
            sources.Update(evaluatedEntity, entityManager, in dispatcher);

            Comparator comparator = GetComparatorFactory().BuildComparator(
                conditions,
                new EffectConditionValueResolver(sources, context));
            return comparator.GetResult();
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
                        getter = new ContextValueGetter(UnitValueCategory.Entity, GetOriginEntity);
                        return true;
                    case TargetEntityKey:
                        getter = new ContextValueGetter(UnitValueCategory.Entity, GetTargetEntity);
                        return true;
                    case OtherEntityKey:
                        getter = new ContextValueGetter(UnitValueCategory.Entity, GetOtherEntity);
                        return true;
                    case PositionKey:
                        getter = new ContextValueGetter(UnitValueCategory.Float3, GetPosition);
                        return true;
                    case TriggerValueKey:
                        getter = new ContextValueGetter(UnitValueCategory.Number, GetTriggerValue);
                        return true;
                    default:
                        return ((IComparatorValueResolver)_unitSources).TryGet(key, out getter);
                }
            }

            private UnitValue GetOriginEntity()
            {
                return _context.HasOriginEntity ? UnitValue.FromEntity(_context.OriginEntity) : UnitValue.None;
            }

            private UnitValue GetTargetEntity()
            {
                return _context.HasTargetEntity ? UnitValue.FromEntity(_context.TargetEntity) : UnitValue.None;
            }

            private UnitValue GetOtherEntity()
            {
                return _context.HasOtherEntity ? UnitValue.FromEntity(_context.OtherEntity) : UnitValue.None;
            }

            private UnitValue GetPosition()
            {
                if (!_context.HasPosition)
                    return UnitValue.None;

                return UnitValue.FromFloat3(new float3(
                    _context.Position.x,
                    _context.Position.y,
                    _context.Position.z));
            }

            private UnitValue GetTriggerValue()
            {
                return UnitValue.FromFloat(_context.TriggerValue);
            }
        }

        private sealed class ContextValueGetter : IParameterizedUnitValueGetter
        {
            private static readonly ComparatorParameterDefinition[] s_parameters = Array.Empty<ComparatorParameterDefinition>();
            private readonly Func<UnitValue> _getValue;

            public ContextValueGetter(UnitValueCategory returnType, Func<UnitValue> getValue)
            {
                ReturnType = returnType;
                _getValue = getValue;
            }

            public UnitValueCategory ReturnType { get; }
            public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;

            public bool TryGet(UnitValue[] parameters, out UnitValue value)
            {
                if (parameters != null && parameters.Length != 0)
                {
                    value = UnitValue.None;
                    return false;
                }

                value = _getValue();
                return value.Category == ReturnType;
            }
        }
    }
}
