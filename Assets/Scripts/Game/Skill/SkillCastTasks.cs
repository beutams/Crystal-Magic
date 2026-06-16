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
        private bool _applied;

        public ApplyRuntimeBuffSkillCastTaskRuntime(ApplyRuntimeBuffSkillCastTaskData data) : base(data?.HookPoint ?? SkillCastHookPoint.BeforeWindup)
        {
            _buffId = data?.BuffId ?? -1;
            _stackCount = math.max(1, data?.StackCount ?? 1);
        }

        public override bool Tick(EntityManager entityManager, Entity entity, ref UnitCastComponent cast, ref float remainingTime)
        {
            if (_applied)
                return true;

            UnitBuffUtility.AddRuntimeBuff(
                entityManager,
                entity,
                _buffId,
                cast.CurrentSkillId,
                _stackCount);
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
                if (!TryStartJump(entityManager, entity))
                    return true;

                _started = true;
            }

            if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitJumpArcComponent>(entity))
                return true;

            UnitJumpArcComponent jump = entityManager.GetComponentData<UnitJumpArcComponent>(entity);
            if (jump.IsActive != 0 && jump.IsCompleted == 0)
                return false;

            return true;
        }

        private bool TryStartJump(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.Exists(entity) ||
                !entityManager.HasComponent<UnitJumpArcComponent>(entity) ||
                !entityManager.HasComponent<Unity.Transforms.LocalTransform>(entity) ||
                !SkillTargetUtility.TryGetTargetPosition(entityManager, entity, out float2 targetPosition))
                return false;

            Unity.Transforms.LocalTransform transform = entityManager.GetComponentData<Unity.Transforms.LocalTransform>(entity);
            Unity.Mathematics.float3 startPosition = transform.Position;
            Unity.Mathematics.float3 endPosition = new(targetPosition.x, targetPosition.y, startPosition.z);

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
                move.ClearTargetMovement();
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
                IsActive = 1,
                IsCompleted = 0,
            };

            entityManager.SetComponentData(entity, jump);
            return true;
        }
    }

    public sealed class RepeatCastWithRetargetSkillCastTaskRuntime : SkillCastTaskRuntime
    {
        private readonly int _additionalCastCount;
        private readonly float _intervalSeconds;
        private readonly bool _retargetBeforeEachCast;
        private float _remainingDelay;
        private int _remainingCasts;

        public RepeatCastWithRetargetSkillCastTaskRuntime(RepeatCastWithRetargetSkillCastTaskData data) : base(data?.HookPoint ?? SkillCastHookPoint.BeforeRecovery)
        {
            _additionalCastCount = math.max(0, data?.AdditionalCastCount ?? 0);
            _intervalSeconds = math.max(0f, data?.IntervalSeconds ?? 0f);
            _retargetBeforeEachCast = data?.RetargetBeforeEachCast ?? true;
            _remainingDelay = _intervalSeconds;
            _remainingCasts = _additionalCastCount;
        }

        public override bool Tick(EntityManager entityManager, Entity entity, ref UnitCastComponent cast, ref float remainingTime)
        {
            while (_remainingCasts > 0)
            {
                if (_remainingDelay > 0f)
                {
                    float consumedTime = math.min(remainingTime, _remainingDelay);
                    _remainingDelay -= consumedTime;
                    remainingTime -= consumedTime;
                    if (_remainingDelay > 0f)
                        return false;
                }

                if (_retargetBeforeEachCast)
                    Retarget(entityManager, entity);

                if (SkillExecutionUtility.TryResolveCurrentSkill(entityManager, entity, cast, out ResolvedSkillData skillData))
                    SkillExecutionUtility.ExecuteResolvedSkillOnce(entityManager, entity, cast, skillData);

                _remainingCasts--;
                if (_remainingCasts <= 0)
                    return true;

                _remainingDelay = _intervalSeconds;
            }

            return true;
        }

        private static void Retarget(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitPerceptionComponent>(entity))
                return;

            UnitPerceptionComponent perception = entityManager.GetComponentData<UnitPerceptionComponent>(entity);
            if (!perception.HasTarget)
                return;

            if (entityManager.HasComponent<UnitIntentComponent>(entity))
            {
                UnitIntentComponent intent = entityManager.GetComponentData<UnitIntentComponent>(entity);
                intent.CastTargetPosition = perception.TargetPosition;
                entityManager.SetComponentData(entity, intent);
            }
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
                RepeatCastWithRetargetSkillCastTaskData repeatCastData => new RepeatCastWithRetargetSkillCastTaskRuntime(repeatCastData),
                _ => null,
            };
        }
    }
}
