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

    public sealed class TurnToTargetSkillCastTaskRuntime : SkillCastTaskRuntime
    {
        private readonly float _durationSeconds;
        private readonly float _turnRateRadiansPerSecond;
        private float _remainingDuration;

        public TurnToTargetSkillCastTaskRuntime(TurnToTargetSkillCastTaskData data) : base(data?.HookPoint ?? SkillCastHookPoint.BeforeRecovery)
        {
            _durationSeconds = math.max(0f, data?.DurationSeconds ?? 0f);
            _turnRateRadiansPerSecond = math.radians(math.max(0f, data?.TurnRateDegreesPerSecond ?? 0f));
            _remainingDuration = _durationSeconds;
        }

        public override bool Tick(EntityManager entityManager, Entity entity, ref UnitCastComponent cast, ref float remainingTime)
        {
            if (_durationSeconds <= 0f)
            {
                UpdateFacing(entityManager, entity, cast, float.MaxValue);
                return true;
            }

            float consumedTime = math.min(remainingTime, _remainingDuration);
            remainingTime -= consumedTime;
            _remainingDuration -= consumedTime;
            UpdateFacing(entityManager, entity, cast, consumedTime);
            return _remainingDuration <= 0f;
        }

        private void UpdateFacing(EntityManager entityManager, Entity entity, in UnitCastComponent cast, float deltaTime)
        {
            if (!TryGetDesiredDirection(entityManager, entity, cast, out float2 desiredDirection))
                return;

            UnitFacingUtility.TryGetFacing(entityManager, entity, out float2 currentFacing);
            if (_turnRateRadiansPerSecond <= 0f || !entityManager.Exists(entity))
            {
                UnitFacingUtility.SetFacing(entityManager, entity, desiredDirection);
                return;
            }

            float maxRadians = _turnRateRadiansPerSecond * math.max(0f, deltaTime);
            float2 nextFacing = RotateTowards(currentFacing, desiredDirection, maxRadians);
            UnitFacingUtility.SetFacing(entityManager, entity, nextFacing);
        }

        private static bool TryGetDesiredDirection(EntityManager entityManager, Entity entity, in UnitCastComponent cast, out float2 desiredDirection)
        {
            desiredDirection = new float2(1f, 0f);
            if (entity == Entity.Null ||
                !entityManager.Exists(entity) ||
                !entityManager.HasComponent<Unity.Transforms.LocalTransform>(entity))
            {
                return false;
            }

            Unity.Transforms.LocalTransform transform = entityManager.GetComponentData<Unity.Transforms.LocalTransform>(entity);
            float2 targetPosition = cast.HasLockedTarget
                ? cast.LockedTargetPosition
                : transform.Position.xy + new float2(1f, 0f);

            if (entityManager.HasComponent<UnitPerceptionComponent>(entity))
            {
                UnitPerceptionComponent perception = entityManager.GetComponentData<UnitPerceptionComponent>(entity);
                if (perception.HasTarget)
                    targetPosition = perception.TargetPosition;
            }

            desiredDirection = targetPosition - transform.Position.xy;
            if (math.lengthsq(desiredDirection) <= 0.0001f)
                return false;

            desiredDirection = math.normalize(desiredDirection);
            return true;
        }

        private static float2 RotateTowards(float2 current, float2 target, float maxRadians)
        {
            float2 normalizedCurrent = math.normalizesafe(current, new float2(1f, 0f));
            float2 normalizedTarget = math.normalizesafe(target, new float2(1f, 0f));
            float currentAngle = math.atan2(normalizedCurrent.y, normalizedCurrent.x);
            float targetAngle = math.atan2(normalizedTarget.y, normalizedTarget.x);
            float delta = DeltaAngleRadians(currentAngle, targetAngle);
            float step = math.clamp(delta, -maxRadians, maxRadians);
            float nextAngle = currentAngle + step;
            return new float2(math.cos(nextAngle), math.sin(nextAngle));
        }

        private static float DeltaAngleRadians(float current, float target)
        {
            float delta = target - current;
            while (delta > math.PI)
                delta -= math.PI * 2f;
            while (delta < -math.PI)
                delta += math.PI * 2f;
            return delta;
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
                    Retarget(entityManager, entity, ref cast);

                if (SkillExecutionUtility.TryResolveCurrentSkill(entityManager, entity, cast, out ResolvedSkillData skillData))
                    SkillExecutionUtility.ExecuteResolvedSkillOnce(entityManager, entity, cast, skillData);

                _remainingCasts--;
                if (_remainingCasts <= 0)
                    return true;

                _remainingDelay = _intervalSeconds;
            }

            return true;
        }

        private static void Retarget(EntityManager entityManager, Entity entity, ref UnitCastComponent cast)
        {
            if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitPerceptionComponent>(entity))
                return;

            UnitPerceptionComponent perception = entityManager.GetComponentData<UnitPerceptionComponent>(entity);
            if (!perception.HasTarget)
                return;

            cast.HasLockedTarget = true;
            cast.LockedTargetPosition = perception.TargetPosition;
            UnitFacingUtility.FaceTowardsPosition(entityManager, entity, perception.TargetPosition);
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
                TurnToTargetSkillCastTaskData turnToTargetData => new TurnToTargetSkillCastTaskRuntime(turnToTargetData),
                RepeatCastWithRetargetSkillCastTaskData repeatCastData => new RepeatCastWithRetargetSkillCastTaskRuntime(repeatCastData),
                _ => null,
            };
        }
    }
}
