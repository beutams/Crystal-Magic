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

    public sealed class JumpArcSkillCastTaskRuntime : SkillCastTaskRuntime
    {
        private readonly float _durationSeconds;
        private readonly float _arcHeight;
        private bool _started;

        public JumpArcSkillCastTaskRuntime(JumpArcSkillCastTaskData data) : base(data?.HookPoint ?? SkillCastHookPoint.BeforeChantEnd)
        {
            _durationSeconds = math.max(0f, data?.DurationSeconds ?? 0f);
            _arcHeight = math.max(0f, data?.ArcHeight ?? 0f);
        }

        public override bool Tick(EntityManager entityManager, Entity entity, ref UnitCastComponent cast, ref float remainingTime)
        {
            if (!_started)
            {
                if (!TryStartJump(entityManager, entity, cast))
                    return true;

                _started = true;
            }

            if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitJumpArcComponent>(entity))
                return true;

            UnitJumpArcComponent jump = entityManager.GetComponentData<UnitJumpArcComponent>(entity);
            if (jump.IsCompleted == 0)
                return false;

            entityManager.RemoveComponent<UnitJumpArcComponent>(entity);
            return true;
        }

        private bool TryStartJump(EntityManager entityManager, Entity entity, in UnitCastComponent cast)
        {
            if (!cast.HasLockedTarget || !entityManager.Exists(entity) || !entityManager.HasComponent<Unity.Transforms.LocalTransform>(entity))
                return false;

            Unity.Transforms.LocalTransform transform = entityManager.GetComponentData<Unity.Transforms.LocalTransform>(entity);
            Unity.Mathematics.float3 startPosition = transform.Position;
            Unity.Mathematics.float3 endPosition = new(cast.LockedTargetPosition.x, cast.LockedTargetPosition.y, startPosition.z);

            if (_durationSeconds <= 0f)
            {
                transform.Position = endPosition;
                transform.Rotation = quaternion.identity;
                entityManager.SetComponentData(entity, transform);
                SkillExecutionUtility.ClearJumpArcState(entityManager, entity);
                return false;
            }

            if (entityManager.HasComponent<UnitMoveComponent>(entity))
            {
                UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(entity);
                move.AccelInput = float2.zero;
                move.Velocity = float2.zero;
                entityManager.SetComponentData(entity, move);
            }

            if (entityManager.HasComponent<Unity.Physics.PhysicsVelocity>(entity))
            {
                Unity.Physics.PhysicsVelocity physicsVelocity = entityManager.GetComponentData<Unity.Physics.PhysicsVelocity>(entity);
                physicsVelocity.Linear = float3.zero;
                physicsVelocity.Angular = float3.zero;
                entityManager.SetComponentData(entity, physicsVelocity);
            }

            UnitJumpArcComponent jump = new()
            {
                StartPosition = startPosition,
                EndPosition = endPosition,
                Duration = _durationSeconds,
                Elapsed = 0f,
                ArcHeight = _arcHeight,
                IsCompleted = 0,
            };

            if (entityManager.HasComponent<UnitJumpArcComponent>(entity))
                entityManager.SetComponentData(entity, jump);
            else
                entityManager.AddComponentData(entity, jump);

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
                JumpArcSkillCastTaskData jumpArcData => new JumpArcSkillCastTaskRuntime(jumpArcData),
                _ => null,
            };
        }
    }
}
