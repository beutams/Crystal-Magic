using System.Collections.Generic;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;

namespace CrystalMagic.Game.Skill
{
    public abstract class SkillCastTaskRuntime
    {
        protected SkillCastTaskRuntime(SkillCastHookPoint hookPoint)
        {
            HookPoint = hookPoint;
        }

        public SkillCastHookPoint HookPoint { get; }

        public abstract bool Tick(EntityManager entityManager, Entity entity, ref UnitCastComponent cast, ref float remainingTime);
    }

    public sealed class DoubleExecuteSkillCastTaskRuntime : SkillCastTaskRuntime
    {
        private readonly SkillModifierSet _runtimeModifiers = new();
        private float _remainingDelay;
        private bool _executed;

        public DoubleExecuteSkillCastTaskRuntime(DoubleExecuteSkillCastTaskData data) : base(data?.HookPoint ?? SkillCastHookPoint.BeforeRecovery)
        {
            _remainingDelay = math.max(0f, data?.DelaySeconds ?? 0f);
            _runtimeModifiers.Add(data?.RuntimeModifiers);
        }

        public override bool Tick(EntityManager entityManager, Entity entity, ref UnitCastComponent cast, ref float remainingTime)
        {
            if (_executed)
                return true;

            if (_remainingDelay > 0f)
            {
                float consumedTime = math.min(remainingTime, _remainingDelay);
                _remainingDelay -= consumedTime;
                remainingTime -= consumedTime;
                if (_remainingDelay > 0f)
                    return false;
            }

            if (!SkillExecutionUtility.TryResolveCurrentSkill(entityManager, entity, cast, out ResolvedSkillData skillData))
                return true;

            SkillExecutionUtility.ExecuteResolvedSkillOnce(entityManager, entity, cast, skillData, _runtimeModifiers);
            _executed = true;
            return true;
        }
    }

    public sealed class ApplyRuntimeBuffSkillCastTaskRuntime : SkillCastTaskRuntime
    {
        private readonly int _buffId;
        private readonly int _stackCount;
        private readonly bool _consumeOnDamageTaken;
        private readonly int _remainingTriggerCount;
        private bool _applied;

        public ApplyRuntimeBuffSkillCastTaskRuntime(ApplyRuntimeBuffSkillCastTaskData data) : base(data?.HookPoint ?? SkillCastHookPoint.BeforeWindup)
        {
            _buffId = data?.BuffId ?? -1;
            _stackCount = math.max(1, data?.StackCount ?? 1);
            _consumeOnDamageTaken = data?.ConsumeOnDamageTaken ?? false;
            _remainingTriggerCount = math.max(1, data?.RemainingTriggerCount ?? 1);
        }

        public override bool Tick(EntityManager entityManager, Entity entity, ref UnitCastComponent cast, ref float remainingTime)
        {
            if (_applied)
                return true;

            UnitBuffUtility.AddRuntimeBuff(
                entityManager,
                entity,
                _buffId,
                cast.CurrentExecutionToken,
                _stackCount,
                _consumeOnDamageTaken,
                _remainingTriggerCount);
            _applied = true;
            return true;
        }
    }

    public static class SkillCastTaskRuntimeFactory
    {
        public static SkillCastTaskRuntime Create(SkillCastTaskData data)
        {
            return data switch
            {
                DoubleExecuteSkillCastTaskData doubleExecuteData => new DoubleExecuteSkillCastTaskRuntime(doubleExecuteData),
                ApplyRuntimeBuffSkillCastTaskData applyRuntimeBuffData => new ApplyRuntimeBuffSkillCastTaskRuntime(applyRuntimeBuffData),
                _ => null,
            };
        }
    }
}
