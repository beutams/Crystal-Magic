using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Requests automatic NPC stories and money pickups through the same interaction transaction.
/// Managed because NPC conditions depend on save data; rewards remain simulation-authoritative.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(UnitNavigationSystem))]
[UpdateBefore(typeof(StateScriptSystem))]
public partial class AutoInteractionSystem : SystemBase
{
    private readonly Dictionary<(Entity Actor, Entity Target), double> _retryAt = new();
    private readonly List<(Entity Actor, Entity Target)> _expired = new();
    private Entity _interactionEntity;
    private EntityQuery _playerQuery;
    private EntityQuery _targetQuery;

    public void DeferRetry(Entity actor, Entity target, float seconds)
    {
        _retryAt[(actor, target)] = World.Time.ElapsedTime + math.max(.5f, seconds);
    }

    protected override void OnCreate()
    {
        _interactionEntity = GameSingletonUtility.GetEntity<GameInteractionComponent>(EntityManager);
        _playerQuery = GetEntityQuery(
            ComponentType.ReadOnly<PlayerInputComponent>(),
            ComponentType.ReadOnly<UnitStateScriptComponent>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.Exclude<UnitInitializationPendingTag>(),
            ComponentType.Exclude<UnitDeathComponent>(),
            ComponentType.Exclude<DestroyEntityFlag>());
        _targetQuery = GetEntityQuery(
            ComponentType.ReadOnly<UnitInteractableComponent>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.Exclude<UnitInitializationPendingTag>(),
            ComponentType.Exclude<UnitDeathComponent>(),
            ComponentType.Exclude<DestroyEntityFlag>());
    }

    protected override void OnUpdate()
    {
        if (_interactionEntity == Entity.Null ||
            !EntityManager.Exists(_interactionEntity))
        {
            return;
        }

        double now = SystemAPI.Time.ElapsedTime;
        var transactions = EntityManager.GetBuffer<InteractionTransactionElement>(_interactionEntity);
        for (int index = 0; index < transactions.Length; index++)
        {
            InteractionTransactionElement transaction = transactions[index];
            if (transaction.IsAutomatic == 0 || transaction.Phase != InteractionPhase.Failed)
                continue;
            float delay = 1f;
            if (!transaction.InteractionKey.IsEmpty && EntityManager.Exists(transaction.Target) &&
                EntityManager.HasComponent<UnitInteractableComponent>(transaction.Target))
            {
                int npcId = EntityManager.GetComponentData<UnitInteractableComponent>(transaction.Target).Data.DataId;
                NPCData npc = DataComponent.Instance.Get<NPCData>(npcId);
                if (npc?.Interactions != null)
                    foreach (NPCInteractionData interaction in npc.Interactions)
                        if (interaction != null && interaction.Key == transaction.InteractionKey.ToString())
                            delay = math.max(0.5f, interaction.RetrySeconds);
            }
            _retryAt[(transaction.Actor, transaction.Target)] = now + delay;
        }
        GameInteractionUtility.AcknowledgeAutomatic(EntityManager, _interactionEntity);
        GameWorldRole role = GameWorldContextUtility.Get(EntityManager).Role;
        if (role != GameWorldRole.Server &&
            (GameGateComponent.Instance.IsPlayerInputLocked ||
             GameGateComponent.Instance.IsSimulationLocked ||
             TransitionComponent.Instance.IsTransitioning))
            return;

        _expired.Clear();
        foreach (var pair in _retryAt)
            if (pair.Value <= now || !EntityManager.Exists(pair.Key.Actor) || !EntityManager.Exists(pair.Key.Target))
                _expired.Add(pair.Key);
        foreach (var key in _expired)
            _retryAt.Remove(key);

        GameSceneMode scene = GameWorldContextUtility.GetSceneMode(EntityManager);
        using NativeArray<Entity> players = _playerQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<Entity> targets = _targetQuery.ToEntityArray(Allocator.Temp);

        for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
        {
            Entity player = players[playerIndex];
            bool combatPlayer = EntityManager.HasComponent<UnitSkillReleaseComponent>(player);
            if (scene != GameSceneMode.None && combatPlayer != (scene is GameSceneMode.Dungeon or GameSceneMode.Training))
                continue;
            if (BattlePlayerStatusUtility.IsInputLocked(EntityManager, player))
                continue;
            float3 playerPosition = EntityManager.GetComponentData<LocalTransform>(player).Position;
            Entity selectedTarget = Entity.Null;
            float nearestDistanceSq = float.MaxValue;
            int selectedPriority = int.MinValue;
            string selectedKey = null;
            float retrySeconds = 1f;

            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                Entity target = targets[targetIndex];
                if (World.GetExistingSystemManaged<StateScriptManagedCommandSystem>()?.HasNpcSession(target) == true)
                    continue;
                if (_retryAt.ContainsKey((player, target)))
                    continue;

                UnitInteractableComponent interactable =
                    EntityManager.GetComponentData<UnitInteractableComponent>(target);
                UnitInteractionData data = interactable.Data;
                if (!GameInteractionTargetUtility.IsAvailable(EntityManager, target, interactable))
                    continue;

                float3 targetPosition = EntityManager.GetComponentData<LocalTransform>(target).Position;
                float distanceSq = math.lengthsq((playerPosition - targetPosition).xy);
                if (interactable.RangeSq > 0f && distanceSq > interactable.RangeSq)
                    continue;

                NPCInteractionData interaction = null;
                int priority = 0;
                if (data.Kind == InteractionKind.Npc && role == GameWorldRole.Standalone && scene == GameSceneMode.Town)
                {
                    NPCData npc = DataComponent.Instance.Get<NPCData>(data.DataId);
                    if (npc != null)
                        foreach (NPCInteractionData candidate in npc.GetEnabledInteractions(automatic: true))
                            if (interaction == null || candidate.AutoPriority > interaction.AutoPriority)
                                interaction = candidate;
                    if (interaction == null)
                        continue;
                    priority = interaction.AutoPriority;
                }
                else if (data.Kind != InteractionKind.Drop ||
                         (DropRewardType)data.Variant != DropRewardType.Money || data.Amount <= 0)
                    continue;

                if (priority > selectedPriority || (priority == selectedPriority && distanceSq < nearestDistanceSq))
                {
                    nearestDistanceSq = distanceSq;
                    selectedTarget = target;
                    selectedPriority = priority;
                    selectedKey = interaction?.Key;
                    retrySeconds = math.max(0.5f, interaction?.RetrySeconds ?? 1f);
                }
            }

            if (selectedTarget != Entity.Null && GameInteractionUtility.TryRequest(
                    EntityManager,
                    _interactionEntity,
                    player,
                    selectedTarget,
                    automatic: true,
                    interactionKey: selectedKey))
                _retryAt[(player, selectedTarget)] = now + retrySeconds;
        }
    }
}
