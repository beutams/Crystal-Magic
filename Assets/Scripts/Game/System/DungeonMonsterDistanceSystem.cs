using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Profiling;
using Stopwatch = System.Diagnostics.Stopwatch;

// Managed boundary: config and network spawn queues are managed. The monster's
// original Entity is retained as its snapshot, so ownership and script references
// never need to be reconstructed and sleeping cannot cause death/drop events.
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup), OrderFirst = true)]
public partial class DungeonMonsterDistanceSystem : SystemBase
{
    private static readonly ProfilerMarker CheckMarker = new("Dungeon.MonsterDistanceCheck");
    private EntityQuery _monsters;
    private EntityQuery _observers;
    private EntityQuery _projectiles;
    private EntityQuery _effects;
    private EntityQuery _commands;
    private readonly List<float3> _positions = new();
    private readonly HashSet<Entity> _projectileReferences = new();
    private double _nextCheck;
    private readonly int[] _blockedCounts = new int[(int)DungeonMonsterSleepBlockReason.NoObserver + 1];
    public DungeonConfig Settings { get; set; }
    public int ActiveCount { get; private set; }
    public int SleepingCount { get; private set; }
    public int ObserverCount => _positions.Count;
    public int ActiveWithinWakeCount { get; private set; }
    public int ActiveInBufferCount { get; private set; }
    public int ActiveFarCount { get; private set; }
    public int WaitingToSleepCount { get; private set; }
    public int DormantMovingCount { get; private set; }
    public double LastCheckMilliseconds { get; private set; }
    public int GetBlockedCount(DungeonMonsterSleepBlockReason reason) => _blockedCounts[(int)reason];

    protected override void OnCreate()
    {
        _monsters = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<DungeonRuntimeOwnedEntity>(),
                ComponentType.ReadOnly<UnitFactionComponent>(), ComponentType.ReadOnly<LocalTransform>() },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        });
        _observers = GetEntityQuery(ComponentType.ReadOnly<UnitFactionComponent>(), ComponentType.ReadOnly<LocalTransform>());
        _projectiles = GetEntityQuery(ComponentType.ReadOnly<SkillProjectilePayloadComponent>());
        _effects = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<EffectEntry>() },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        });
        _commands = GetEntityQuery(ComponentType.ReadOnly<StateScriptManagedCommandElement>());
        RequireForUpdate<DungeonFloorControllerComponent>();
    }

    protected override void OnStartRunning()
    {
        Settings ??= ConfigComponent.Instance.Get<DungeonConfig>();
        Settings.EnsureValid();
        _nextCheck = 0;
    }

    protected override void OnUpdate()
    {
        double now = SystemAPI.Time.ElapsedTime;
        if (now < _nextCheck) return;
        using var checkScope = CheckMarker.Auto();
        long checkStarted = Stopwatch.GetTimestamp();
        _nextCheck = now + Settings.MonsterDistanceCheckInterval;
        EntityManager.CompleteAllTrackedJobs();
        GatherObservers();
        GatherProjectileReferences();
        ActiveCount = SleepingCount = 0;
        ActiveWithinWakeCount = ActiveInBufferCount = ActiveFarCount = WaitingToSleepCount = DormantMovingCount = 0;
        System.Array.Clear(_blockedCounts, 0, _blockedCounts.Length);
        using NativeArray<Entity> monsters = _monsters.ToEntityArray(Allocator.Temp);
        foreach (Entity entity in monsters)
        {
            UnitFactionType faction = EntityManager.GetComponentData<UnitFactionComponent>(entity).Value;
            if (faction != UnitFactionType.Enemy && faction != UnitFactionType.Boss) continue;
            bool tracked = EntityManager.HasComponent<DungeonMonsterDistanceState>(entity);
            DungeonMonsterDistanceState state = tracked
                ? EntityManager.GetComponentData<DungeonMonsterDistanceState>(entity)
                : new DungeonMonsterDistanceState { FarSince = -1 };
            // Never take ownership of another system's Disabled entity.
            if (EntityManager.HasComponent<Disabled>(entity) && state.Sleeping == 0) continue;
            if (!tracked) EntityManager.AddComponentData(entity, state);

            float health = EntityManager.HasComponent<UnitVitalityComponent>(entity)
                ? EntityManager.GetComponentData<UnitVitalityComponent>(entity).CurrentHealth : 1f;
            bool damaged = state.HasHealth != 0 && health < state.LastHealth;
            state.LastHealth = health;
            state.HasHealth = 1;
            float distanceSq = NearestDistanceSq(EntityManager.GetComponentData<LocalTransform>(entity).Position);
            bool terminal = IsTerminal(entity) || health <= 0f;
            bool wasSleeping = state.Sleeping != 0;
            DungeonMonsterSleepBlockReason blockReason = DungeonMonsterSleepBlockReason.None;
            if (state.Sleeping != 0)
            {
                // A new effect or a cleared patrol owner must still run normal gameplay.
                if (!Settings.MonsterDistanceSleepEnabled || terminal || damaged || HasPendingEffects(entity) ||
                    UnitDamageAggroUtility.HasActiveTarget(EntityManager, entity, now) ||
                    _projectileReferences.Contains(entity) || PatrolOwnerCleared(entity) ||
                    distanceSq <= Settings.MonsterWakeDistance * Settings.MonsterWakeDistance)
                    Wake(entity, ref state);
                else
                    PrepareDormantTravel(entity);
            }
            else
            {
                bool far = distanceSq > Settings.MonsterSleepDistance * Settings.MonsterSleepDistance;
                if (far && !terminal) blockReason = GetFarBlockReason(entity, damaged);
                bool eligible = far && !terminal && blockReason == DungeonMonsterSleepBlockReason.None;
                if (!eligible) state.FarSince = -1;
                else
                {
                    if (state.FarSince < 0) state.FarSince = now;
                    if (now - state.FarSince >= Settings.MonsterSleepDelay)
                    {
                        NetworkEntitySpawnUtility.SuspendEntity(EntityManager, entity);
                        ClearPerception(entity);
                        state.Sleeping = 1;
                        EntityManager.SetEnabled(entity, false);
                        PrepareDormantTravel(entity);
                    }
                }
            }
            EntityManager.SetComponentData(entity, state);
            if (state.Sleeping != 0)
            {
                SleepingCount++;
                if (EntityManager.HasComponent<UnitNavigationComponent>(entity))
                {
                    UnitNavigationComponent navigation = EntityManager.GetComponentData<UnitNavigationComponent>(entity);
                    float3 position = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                    if (navigation.HasDestination != 0 &&
                        math.distancesq(position.xy, navigation.Destination.xy) > navigation.StopDistance * navigation.StopDistance)
                        DormantMovingCount++;
                }
            }
            else if (!terminal)
            {
                ActiveCount++;
                if (distanceSq <= Settings.MonsterWakeDistance * Settings.MonsterWakeDistance)
                    ActiveWithinWakeCount++;
                else if (distanceSq <= Settings.MonsterSleepDistance * Settings.MonsterSleepDistance)
                    ActiveInBufferCount++;
                else
                {
                    ActiveFarCount++;
                    DungeonMonsterSleepBlockReason reason = wasSleeping ? GetFarBlockReason(entity, damaged) : blockReason;
                    if (reason == DungeonMonsterSleepBlockReason.None) WaitingToSleepCount++;
                    else _blockedCounts[(int)reason]++;
                }
            }
        }
        LastCheckMilliseconds = (Stopwatch.GetTimestamp() - checkStarted) * 1000d / Stopwatch.Frequency;
    }

    private void GatherObservers()
    {
        _positions.Clear();
        using NativeArray<Entity> entities = _observers.ToEntityArray(Allocator.Temp);
        foreach (Entity entity in entities)
        {
            if (EntityManager.GetComponentData<UnitFactionComponent>(entity).Value != UnitFactionType.Player) continue;
            // Spectators still observe the world and must not see units disappear.
            if (IsTerminal(entity) && !EntityManager.HasComponent<BattleSpectatorComponent>(entity)) continue;
            _positions.Add(EntityManager.GetComponentData<LocalTransform>(entity).Position);
        }
    }

    private void GatherProjectileReferences()
    {
        _projectileReferences.Clear();
        using NativeArray<SkillProjectilePayloadComponent> projectiles = _projectiles.ToComponentDataArray<SkillProjectilePayloadComponent>(Allocator.Temp);
        foreach (SkillProjectilePayloadComponent projectile in projectiles)
        {
            _projectileReferences.Add(projectile.Context.OriginEntity);
            _projectileReferences.Add(projectile.Context.TargetEntity);
        }
        using NativeArray<Entity> queues = _effects.ToEntityArray(Allocator.Temp);
        foreach (Entity queue in queues)
            foreach (EffectEntry effect in EntityManager.GetBuffer<EffectEntry>(queue, true))
            {
                _projectileReferences.Add(effect.Context.OriginEntity);
                _projectileReferences.Add(effect.Context.TargetEntity);
                _projectileReferences.Add(effect.Context.OtherEntity);
            }
        using NativeArray<Entity> commandQueues = _commands.ToEntityArray(Allocator.Temp);
        foreach (Entity queue in commandQueues)
            foreach (StateScriptManagedCommandElement command in EntityManager.GetBuffer<StateScriptManagedCommandElement>(queue, true))
            {
                _projectileReferences.Add(command.SourceEntity);
                _projectileReferences.Add(command.TargetEntity);
            }
    }

    private float NearestDistanceSq(float3 position)
    {
        float result = float.PositiveInfinity;
        foreach (float3 observer in _positions) result = math.min(result, math.distancesq(position.xy, observer.xy));
        return result;
    }

    private bool IsTerminal(Entity entity) =>
        (EntityManager.HasComponent<UnitDeathComponent>(entity) && EntityManager.IsComponentEnabled<UnitDeathComponent>(entity)) ||
        (EntityManager.HasComponent<DestroyEntityFlag>(entity) && EntityManager.IsComponentEnabled<DestroyEntityFlag>(entity));

    private bool HasPendingEffects(Entity entity) =>
        HasEntries<UnitBuffElement>(entity) || HasEntries<UnitBuffHookRequestElement>(entity) ||
        HasEntries<EffectEntry>(entity) || HasEntries<StateScriptSourceCommandElement>(entity) ||
        (EntityManager.HasComponent<UnitControlRuntimeComponent>(entity) &&
         EntityManager.GetComponentData<UnitControlRuntimeComponent>(entity).HasControl != 0);

    private bool HasEntries<T>(Entity entity) where T : unmanaged, IBufferElementData =>
        EntityManager.HasBuffer<T>(entity) && EntityManager.GetBuffer<T>(entity, true).Length > 0;

    private DungeonMonsterSleepBlockReason GetFarBlockReason(Entity entity, bool damaged) =>
        !Settings.MonsterDistanceSleepEnabled ? DungeonMonsterSleepBlockReason.FeatureDisabled
        : _positions.Count == 0 ? DungeonMonsterSleepBlockReason.NoObserver
        : damaged ? DungeonMonsterSleepBlockReason.RecentDamage : GetProtectionReason(entity);

    private DungeonMonsterSleepBlockReason GetProtectionReason(Entity entity)
    {
        if (EntityManager.HasComponent<UnitInitializationPendingTag>(entity) ||
            EntityManager.HasComponent<UnitSpawnInitializationComponent>(entity)) return DungeonMonsterSleepBlockReason.Initializing;
        if (HasEntries<UnitBuffElement>(entity)) return DungeonMonsterSleepBlockReason.Buff;
        if (EntityManager.HasComponent<UnitControlRuntimeComponent>(entity) &&
            EntityManager.GetComponentData<UnitControlRuntimeComponent>(entity).HasControl != 0) return DungeonMonsterSleepBlockReason.Control;
        if (HasEntries<UnitBuffHookRequestElement>(entity) || HasEntries<EffectEntry>(entity) ||
            HasEntries<StateScriptSourceCommandElement>(entity)) return DungeonMonsterSleepBlockReason.PendingEffect;
        if (_projectileReferences.Contains(entity)) return DungeonMonsterSleepBlockReason.Referenced;
        if (Bool(entity, "ai.attack.locked") || Bool(entity, "dungeon.patrol.engaged") ||
            UnitDamageAggroUtility.HasActiveTarget(EntityManager, entity, SystemAPI.Time.ElapsedTime))
            return DungeonMonsterSleepBlockReason.Combat;
        StateScriptManagedCommandSystem scripts = World.GetExistingSystemManaged<StateScriptManagedCommandSystem>();
        if (scripts != null && scripts.HasRunningActions(entity)) return DungeonMonsterSleepBlockReason.RunningAction;
        if (!EntityManager.HasBuffer<UnitPerceptionUnitElement>(entity)) return DungeonMonsterSleepBlockReason.None;
        foreach (UnitPerceptionUnitElement other in EntityManager.GetBuffer<UnitPerceptionUnitElement>(entity, true))
            if (other.Faction == UnitFactionType.Player && EntityManager.Exists(other.Value) && !IsTerminal(other.Value)) return DungeonMonsterSleepBlockReason.Perception;
        return DungeonMonsterSleepBlockReason.None;
    }

    private void ClearPerception(Entity entity)
    {
        if (EntityManager.HasBuffer<UnitPerceptionUnitElement>(entity))
            EntityManager.GetBuffer<UnitPerceptionUnitElement>(entity).Clear();
    }

    private bool Bool(Entity entity, string key) => EntityManager.Exists(entity) &&
        UnitVariableSource.TryGetValue(EntityManager, entity, key, out UnitSourceValue value) &&
        value.TryGetBool(out bool flag) && flag;

    private bool PatrolOwnerCleared(Entity entity) => Bool(entity, DungeonPatrolRuntimeUtility.PatrolMemberKey) &&
        EntityManager.HasComponent<UnitOwnerComponent>(entity) &&
        DungeonPatrolRuntimeUtility.IsEncounterDead(EntityManager, EntityManager.GetComponentData<UnitOwnerComponent>(entity).Owner);

    private void PrepareDormantTravel(Entity entity)
    {
        if (!EntityManager.HasComponent<UnitNavigationComponent>(entity) || !EntityManager.HasComponent<UnitMoveComponent>(entity)) return;
        UnitNavigationComponent navigation = EntityManager.GetComponentData<UnitNavigationComponent>(entity);
        UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(entity);
        Entity owner = EntityManager.HasComponent<UnitOwnerComponent>(entity)
            ? EntityManager.GetComponentData<UnitOwnerComponent>(entity).Owner : Entity.Null;
        bool hasDestination = false;
        if (Bool(entity, DungeonPatrolRuntimeUtility.PatrolMemberKey))
        {
            if (EntityManager.Exists(owner) && EntityManager.HasComponent<DungeonInterestPointComponent>(owner) &&
                Bool(owner, DungeonPatrolRuntimeUtility.PatrolActiveKey))
            {
                DungeonInterestPointComponent point = EntityManager.GetComponentData<DungeonInterestPointComponent>(owner);
                if (EntityManager.Exists(point.PatrolTarget) && EntityManager.HasComponent<LocalTransform>(point.PatrolTarget))
                {
                    UnitNavigationUtility.SetDestination(ref navigation,
                        EntityManager.GetComponentData<LocalTransform>(point.PatrolTarget).Position, math.max(0.05f, point.ArrivalDistance));
                    move.CommandMoveSpeed = math.max(0f, point.PatrolSpeed);
                    hasDestination = true;
                }
            }
        }
        else if (Bool(entity, "dungeon.revenge.member") && _positions.Count > 0)
        {
            // An offscreen reinforcement still advances towards a player.
            float3 current = EntityManager.GetComponentData<LocalTransform>(entity).Position;
            float3 target = _positions[0];
            foreach (float3 position in _positions)
                if (math.distancesq(current.xy, position.xy) < math.distancesq(current.xy, target.xy)) target = position;
            UnitNavigationUtility.SetDestination(ref navigation, target, 1f);
            move.CommandMoveSpeed = -1f;
            hasDestination = true;
        }
        else if (UnitVariableSource.TryGetValue(EntityManager, entity, DungeonPatrolRuntimeUtility.GuardHomePositionKey,
                     out UnitSourceValue home) && home.TryGetFloat3(out float3 position))
        {
            UnitNavigationUtility.SetDestination(ref navigation, position, 0.15f);
            move.CommandMoveSpeed = -1f;
            hasDestination = true;
        }
        if (!hasDestination) UnitNavigationUtility.Stop(ref navigation);
        move.Direction = float2.zero;
        move.Velocity = float2.zero;
        EntityManager.SetComponentData(entity, navigation);
        EntityManager.SetComponentData(entity, move);
    }

    private void Wake(Entity entity, ref DungeonMonsterDistanceState state)
    {
        EntityManager.SetEnabled(entity, true);
        state.Sleeping = 0;
        state.FarSince = -1;
        if (EntityManager.HasComponent<UnitNavigationComponent>(entity))
        {
            UnitNavigationComponent navigation = EntityManager.GetComponentData<UnitNavigationComponent>(entity);
            navigation.PathDirty = 1;
            EntityManager.SetComponentData(entity, navigation);
        }
        if (EntityManager.HasComponent<UnitMoveComponent>(entity))
        {
            UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(entity);
            move.NetworkDirty = 1;
            EntityManager.SetComponentData(entity, move);
        }
        if (EntityManager.HasComponent<UnitFacingComponent>(entity))
        {
            UnitFacingComponent facing = EntityManager.GetComponentData<UnitFacingComponent>(entity);
            facing.NetworkDirty = 1;
            EntityManager.SetComponentData(entity, facing);
        }
        if (EntityManager.HasComponent<UnitAnimationComponent>(entity))
        {
            UnitAnimationComponent animation = EntityManager.GetComponentData<UnitAnimationComponent>(entity);
            animation.NetworkDirty = 1;
            EntityManager.SetComponentData(entity, animation);
        }
        NetworkEntitySpawnUtility.ResumeEntity(EntityManager, entity);
    }
}
