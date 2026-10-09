using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using CrystalMagic.Game.Unit;
using CrystalMagic.UI;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation |
                   WorldSystemFilterFlags.ClientSimulation |
                   WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(StateScriptSystem))]
public partial class StateScriptManagedCommandSystem : SystemBase
{
    private const string NpcSessionInputLockReason = "StateScript.NpcSession";

    private static readonly ProfilerMarker WaitForScheduledJobsMarker =
        new("StateScript.Managed.WaitForScheduledJobs");
    private static readonly ProfilerMarker UpdateSourceDispatcherMarker =
        new("StateScript.Managed.UpdateSourceDispatcher");
    private static readonly ProfilerMarker TickRunningActionsMarker =
        new("StateScript.Managed.TickRunningActions");
    private static readonly ProfilerMarker TickNpcSessionsMarker =
        new("StateScript.Managed.TickNpcSessions");
    private static readonly ProfilerMarker ResolveSourcesMarker =
        new("StateScript.Managed.ResolveSources");
    private static readonly ProfilerMarker CopyCommandQueueMarker =
        new("StateScript.Managed.CopyCommandQueue");
    private static readonly ProfilerMarker<int> ExecuteCommandsMarker =
        new("StateScript.Managed.ExecuteCommands", "Command Count");
    private static readonly ProfilerMarker TraceCommandsMarker =
        new("StateScript.Managed.TraceCommands");
    private static readonly ProfilerMarker AddMissingDestroyFlagsMarker =
        new("StateScript.Managed.AddMissingDestroyFlags");

    private sealed class RunningAddition
    {
        public uint ExecutionVersion;
        public List<SkillAdditionAction> Actions;
    }

    private readonly Dictionary<StateScriptActionKey, RunningAddition> _runningActions = new();
    private readonly Dictionary<StateScriptActionKey, StateScriptSpawnBatch> _spawnBatches = new();
    private readonly List<StateScriptActionKey> _completedSpawnBatches = new();
    private readonly List<StateScriptActionKey> _completedKeys = new();
    private readonly Dictionary<StateScriptEffectKey, EffectDataListId> _effectLists = new();
    private readonly List<Entity> _missingDestroyFlags = new();
    private readonly SkillContent _skillContext = new();
    private readonly Dictionary<Entity, int> _castOrdinals = new();
    private uint _castOrdinalFrame;
    private readonly Dictionary<Entity, NPCInteractionSession> _npcSessions = new();
    private readonly List<Entity> _completedNpcTargets = new();
    private UnitSourceDispatcher _sourceDispatcher;
    private EntityQuery _commandQueueQuery;
    private Entity _commandQueueEntity;
    private Entity _interactionEntity;
    private NPCInteractionNodeRunnerFactory _npcRunnerFactory;
    private bool _npcInputLocked;
    private bool _tickingNpcSessions;
    private GameWorldRole _worldRole;
    private BattleSimulationScope _scope;
    private bool _replayClientSkills;

    public bool HasNpcInteraction => _npcSessions.Count > 0;
    public bool HasNpcSession(Entity target) => _npcSessions.ContainsKey(target);
    public bool BlocksInventoryInput
    {
        get
        {
            foreach (NPCInteractionSession session in _npcSessions.Values)
            {
                if (!session.IsActive) continue;
                if (!session.Interaction.IsSequence ||
                    session.GetCurrentNode() is not NPCWaitConditionInteractionNodeData { AllowInventory: true })
                    return true;
            }
            return false;
        }
    }

    public void CancelNpcInteraction(Entity target)
    {
        if (_npcSessions.TryGetValue(target, out NPCInteractionSession session))
        {
            FinishNpcSession(session, true);
            // EnterDungeon can synchronously leave Town while this dictionary is being ticked.
            if (!_tickingNpcSessions) _npcSessions.Remove(target);
            RefreshNpcInput();
        }
        else
            GameInteractionUtility.FailTarget(EntityManager, _interactionEntity, target);
    }

    public void ReplayClientSkills()
    {
        if (_worldRole != GameWorldRole.Client)
            return;
        _replayClientSkills = true;
        try { Update(); }
        finally { _replayClientSkills = false; }
    }

    public bool HasRunningActions(Entity entity)
    {
        foreach (StateScriptActionKey key in _runningActions.Keys)
            if (key.Entity == entity) return true;
        foreach (StateScriptActionKey key in _spawnBatches.Keys)
            if (key.Entity == entity) return true;
        return false;
    }

    protected override void OnCreate()
    {
        _worldRole = GameWorldContextUtility.Get(EntityManager).Role;
        _sourceDispatcher.Initialize(this);
        _commandQueueEntity = StateScriptManagedCommandQueueUtility.GetOrCreateEntity(EntityManager);
        _commandQueueQuery = GetEntityQuery(
            ComponentType.ReadOnly<StateScriptManagedCommandQueueComponent>(),
            ComponentType.ReadWrite<StateScriptManagedCommandElement>());
        _interactionEntity = GameSingletonUtility.GetEntity<GameInteractionComponent>(EntityManager);
        _npcRunnerFactory = new NPCInteractionNodeRunnerFactory();
        NPCInteractionNodeRunnerRegistry.RegisterAll(_npcRunnerFactory);
        RegisterEffectLists();
        RequireForUpdate<StateScriptRuntimeRegistryComponent>();
        RequireForUpdate<StateScriptManagedCommandQueueComponent>();
    }

    protected override void OnUpdate()
    {
        SystemAPI.TryGetSingleton(out _scope);
        using (WaitForScheduledJobsMarker.Auto())
            Dependency.Complete();

        BlobAssetReference<StateScriptRuntimeRegistryBlob> registry =
            SystemAPI.GetSingleton<StateScriptRuntimeRegistryComponent>().Value;
        if (!registry.IsCreated)
            return;

        // Spawning makes structural changes; refresh source lookups afterwards.
        if (!_replayClientSkills)
            TickSpawnBatches(SystemAPI.Time.DeltaTime);
        using (UpdateSourceDispatcherMarker.Auto())
            _sourceDispatcher.Update(this);
        using (TickRunningActionsMarker.Auto())
            TickRunningActions();
        using (TickNpcSessionsMarker.Auto())
            if (!_replayClientSkills && _scope.Pass != BattleSimulationPass.Players)
                TickNpcSessions(SystemAPI.Time.DeltaTime);

        _missingDestroyFlags.Clear();
        DynamicBuffer<StateScriptManagedCommandElement> commandQueue =
            _commandQueueQuery.GetSingletonBuffer<StateScriptManagedCommandElement>();
        if (!commandQueue.IsEmpty)
        {
            NativeArray<StateScriptManagedCommandElement> pendingCommands;
            using (CopyCommandQueueMarker.Auto())
            {
                pendingCommands = commandQueue.ToNativeArray(Allocator.Temp);
                commandQueue.Clear();
            }

            using (pendingCommands)
            using (ExecuteCommandsMarker.Auto(pendingCommands.Length))
            {
                Entity currentEntity = Entity.Null;
                int currentDefinitionIndex = -1;
                bool currentEntityCanExecute = false;
                UnitSourceResolver resolver = null;
                for (int commandIndex = 0; commandIndex < pendingCommands.Length; commandIndex++)
                {
                    StateScriptManagedCommandElement command = pendingCommands[commandIndex];
                    // 重演技能附加的本地状态及完成回执；交互、转场等操作只处理原始输入。
                    if (_replayClientSkills && command.Type is not
                        (StateScriptManagedCommandType.StartAddition or StateScriptManagedCommandType.StopAddition))
                        continue;
                    Entity entity = command.SourceEntity;
                    if (entity != currentEntity)
                    {
                        currentEntity = entity;
                        currentDefinitionIndex = -1;
                        currentEntityCanExecute = TryPrepareEntity(
                            entity,
                            in registry,
                            out currentDefinitionIndex);
                        resolver = null;
                    }

                    if (!currentEntityCanExecute)
                        continue;

                    ref StateScriptUnitDefinitionBlob unit =
                        ref registry.Value.Units[currentDefinitionIndex];
                    if ((uint)command.GraphIndex >= (uint)unit.Graphs.Length)
                        continue;
                    ref StateScriptGraphDefinitionBlob graph = ref unit.Graphs[command.GraphIndex];
                    if ((uint)command.NodeIndex >= (uint)graph.Nodes.Length)
                        continue;
                    ref StateScriptNodeDefinition node = ref graph.Nodes[command.NodeIndex];
                    ExecuteCommand(entity, command, ref graph, ref node, ref resolver);
                }
            }
        }

        using (AddMissingDestroyFlagsMarker.Auto())
            AddMissingDestroyFlags();
    }

    protected override void OnDestroy()
    {
        ResetScene();
        foreach (EffectDataListId effectListId in _effectLists.Values)
            EffectDataBridgeUtility.Unregister(EntityManager, effectListId);
        _effectLists.Clear();
    }

    public void ResetScene()
    {
        _castOrdinals.Clear();
        if (_commandQueueEntity != Entity.Null && EntityManager.Exists(_commandQueueEntity))
            EntityManager.GetBuffer<StateScriptManagedCommandElement>(_commandQueueEntity).Clear();
        foreach (KeyValuePair<StateScriptActionKey, RunningAddition> pair in _runningActions)
            StopActions(pair.Value.Actions);
        _runningActions.Clear();
        foreach (KeyValuePair<StateScriptActionKey, StateScriptSpawnBatch> pair in _spawnBatches)
            SetPendingSpawnCount(pair.Key.Entity, pair.Value.Node.Text.ToString(), 0);
        _spawnBatches.Clear();
        _completedSpawnBatches.Clear();
        foreach (NPCInteractionSession session in _npcSessions.Values)
            session.Cancel();
        _npcSessions.Clear();
        _completedKeys.Clear();
        _completedNpcTargets.Clear();
        _missingDestroyFlags.Clear();
        ReleaseNpcInput();
    }

    private bool TryPrepareEntity(
        Entity entity,
        in BlobAssetReference<StateScriptRuntimeRegistryBlob> registry,
        out int definitionIndex)
    {
        definitionIndex = -1;
        if (entity == Entity.Null ||
            !EntityManager.Exists(entity) ||
            EntityManager.HasComponent<UnitInitializationPendingTag>(entity) ||
            !EntityManager.HasComponent<UnitStateScriptComponent>(entity))
        {
            return false;
        }

        UnitStateScriptComponent component = EntityManager.GetComponentData<UnitStateScriptComponent>(entity);
        definitionIndex = component.DefinitionIndex;
        if (definitionIndex < 0 || definitionIndex >= registry.Value.Units.Length)
            return false;

        if (component.IsStoppedForDeath == 0 &&
            !EntityManager.HasComponent<BattleSpectatorComponent>(entity) &&
            (!EntityManager.HasComponent<UnitDeathComponent>(entity) ||
             !EntityManager.IsComponentEnabled<UnitDeathComponent>(entity)))
        {
            return true;
        }

        StopActions(entity);
        return false;
    }

    private void RegisterEffectLists()
    {
        DataTable<StateScriptData> table = DataComponent.Instance.GetTable<StateScriptData>();
        if (table == null)
            return;

        List<StateScriptData> rows = new(table.GetAll());
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            StateScriptData row = rows[rowIndex];
            if (row == null)
                continue;
            row.EnsureValid();
            for (int graphIndex = 0; graphIndex < row.Graphs.Count; graphIndex++)
            {
                StateScriptInstanceData graph = row.Graphs[graphIndex];
                if (graph?.Nodes == null)
                    continue;
                for (int nodeIndex = 0; nodeIndex < graph.Nodes.Count; nodeIndex++)
                {
                    if (graph.Nodes[nodeIndex] is not ExecuteEffectActionNodeData executeEffect ||
                        executeEffect.Effects == null ||
                        executeEffect.Effects.Length == 0)
                    {
                        continue;
                    }

                    EffectDataListId effectListId = EffectDataBridgeUtility.Register(
                        EntityManager,
                        executeEffect.Effects, registry: true);
                    if (effectListId.IsValid)
                    {
                        _effectLists[new StateScriptEffectKey(row.Id, graphIndex, nodeIndex)] = effectListId;
                    }
                }
            }
        }
    }

    private void ExecuteEffects(Entity entity, StateScriptManagedCommandElement command)
    {
        UnitStateScriptComponent component = EntityManager.GetComponentData<UnitStateScriptComponent>(entity);
        if (!_effectLists.TryGetValue(
                new StateScriptEffectKey(component.UnitDataId, command.GraphIndex, command.NodeIndex),
                out EffectDataListId effectListId))
        {
            return;
        }

        SkillContent context = EffectUtility.GetContext(EntityManager, in command.EffectContext);
        try
        {
            EffectUtility.Enqueue(
                EntityManager,
                effectListId,
                context,
                math.max(1, command.IntValue));
        }
        finally
        {
            EffectUtility.ReturnContext(context);
        }
    }

    private void MarkForDestroy(Entity entity)
    {
        if (EntityManager.HasComponent<DestroyEntityFlag>(entity))
        {
            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
            return;
        }

        _missingDestroyFlags.Add(entity);
    }

    private void AddMissingDestroyFlags()
    {
        for (int index = 0; index < _missingDestroyFlags.Count; index++)
        {
            Entity entity = _missingDestroyFlags[index];
            if (!EntityManager.Exists(entity))
                continue;
            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);
            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
        _missingDestroyFlags.Clear();
    }

    private void ExecuteCommand(
        Entity entity,
        StateScriptManagedCommandElement command,
        ref StateScriptGraphDefinitionBlob graph,
        ref StateScriptNodeDefinition node,
        ref UnitSourceResolver resolver)
    {
        switch (command.Type)
        {
            case StateScriptManagedCommandType.RequestSkill:
                if (_worldRole != GameWorldRole.Client)
                    EnqueueSkillEffects(entity, command, false);
                break;
            case StateScriptManagedCommandType.RequestSkillWithAddition:
                if (_worldRole != GameWorldRole.Client)
                    EnqueueSkillEffects(entity, command, true);
                break;
            case StateScriptManagedCommandType.NotifyUI:
                NotificationSignalUtility.Publish(EntityManager, entity, node.NotificationKey.ToString(), command.Position);
                break;
            case StateScriptManagedCommandType.PublishGameEvent:
                if (EventComponent.TryGetInstance(out EventComponent eventComponent))
                {
                    eventComponent.Publish(new CommonGameEvent(
                        node.Text.ToString(),
                        new GameplayEventReference(entity, command.Value.ToUnitValue())));
                }
                break;
            case StateScriptManagedCommandType.RequestInteraction:
                GameInteractionUtility.TryRequest(EntityManager, _interactionEntity, entity, command.TargetEntity,
                    requestAllowed: NPCSequenceUtility.AllowsInteraction(EntityManager, command.TargetEntity));
                break;
            case StateScriptManagedCommandType.CompleteInteraction:
                GameInteractionUtility.Complete(
                    EntityManager,
                    _interactionEntity,
                    entity,
                    (InteractionResultCode)command.IntValue,
                    command.Value);
                break;
            case StateScriptManagedCommandType.AcknowledgeInteraction:
                GameInteractionUtility.Acknowledge(EntityManager, _interactionEntity, entity);
                break;
            case StateScriptManagedCommandType.CollectInteraction:
                CollectInteraction(entity);
                break;
            case StateScriptManagedCommandType.StartNpcInteraction:
                StartNpcInteraction(entity);
                break;
            case StateScriptManagedCommandType.SpawnUnit:
                SpawnUnits(entity, command.GraphIndex, command.NodeIndex, command.IntValue, ref graph, in node);
                if (resolver != null)
                    RefreshResolver(entity, resolver);
                break;
            case StateScriptManagedCommandType.ExecuteEffect:
                ExecuteEffects(entity, command);
                break;
            case StateScriptManagedCommandType.DestroySelf:
                GameInteractionUtility.FailTarget(EntityManager, _interactionEntity, entity);
                MarkForDestroy(entity);
                break;
            case StateScriptManagedCommandType.StartAddition:
                StartAddition(
                    entity,
                    command.GraphIndex,
                    command.NodeIndex,
                    command.ExecutionVersion,
                    in node,
                    GetOrCreateResolver(entity, ref resolver));
                RefreshResolver(entity, resolver);
                break;
            case StateScriptManagedCommandType.StopAddition:
                StopAddition(
                    new StateScriptActionKey(entity, command.GraphIndex, command.NodeIndex),
                    command.ExecutionVersion);
                break;
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
            case StateScriptManagedCommandType.TraceTimerStarted:
                using (TraceCommandsMarker.Auto())
                {
                    Debug.Log(
                        $"[StateScriptTrace] Timer started: Entity={entity}, Graph='{graph.Name}', Node='{node.Guid}', " +
                        $"Duration={command.Position.x:0.###}, DeltaTime={command.Position.y:0.######}, Tick={command.IntValue}.");
                }
                break;
            case StateScriptManagedCommandType.TraceTimerCompleted:
                using (TraceCommandsMarker.Auto())
                {
                    Debug.Log(
                        $"[StateScriptTrace] Timer completed: Entity={entity}, Graph='{graph.Name}', Node='{node.Guid}', " +
                        $"Elapsed={command.Position.x:0.###}, Duration={command.Position.y:0.###}, " +
                        $"DeltaTime={command.Position.z:0.######}, Tick={command.IntValue}.");
                }
                break;
            case StateScriptManagedCommandType.TraceCurrentInputType:
                using (TraceCommandsMarker.Auto())
                {
                    Debug.Log(
                        $"[StateScriptTrace] Current input comparison: Entity={entity}, Graph='{graph.Name}', " +
                        $"Node='{node.Guid}', Result={command.Value.Bool != 0}, " +
                        $"SkillId={command.Position.x:0}, CurrentInputType={command.Position.y:0}, " +
                        $"DirectInputType={command.Position.z:0}, SuccessMask={command.IntValue}.");
                }
                break;
            case StateScriptManagedCommandType.TraceSkillRequestBuilt:
                using (TraceCommandsMarker.Auto())
                {
                    Debug.Log(
                        $"[StateScriptTrace] Skill request built: Entity={entity}, Graph='{graph.Name}', Node='{node.Guid}', " +
                        $"SkillId={command.IntValue}, Position={command.Position}, Target={command.TargetEntity}.");
                }
                break;
            case StateScriptManagedCommandType.TraceSkillRequestFailed:
                using (TraceCommandsMarker.Auto())
                {
                    Debug.LogError(
                        $"[StateScriptTrace] Skill request build failed: Entity={entity}, Graph='{graph.Name}', " +
                        $"Node='{node.Guid}', Reason={(StateScriptSkillRequestBuildError)command.IntValue}.");
                }
                break;
            case StateScriptManagedCommandType.TraceAdditionResultConsumed:
                using (TraceCommandsMarker.Auto())
                {
                    Debug.Log(
                        $"[StateScriptAdditionTrace] Completion consumed: Entity={entity}, Graph='{graph.Name}', " +
                        $"Node='{node.Guid}', Version={command.ExecutionVersion}.");
                }
                break;
#endif
        }
    }

    private void EnqueueSkillEffects(Entity entity, StateScriptManagedCommandElement command, bool withAddition)
    {
        if (!EntityManager.HasComponent<UnitSkillReleaseComponent>(entity))
            return;

        SkillModifierSet modifiers = withAddition
            ? PlayerCurrentSkillUtility.ConsumePendingExtraModifiers(EntityManager, entity)
            : new SkillModifierSet();
        SkillReleaseRequest request = SkillReleaseRequestUtility.Create(
            EntityManager,
            entity,
            command.IntValue,
            modifiers,
            command.Position,
            command.TargetEntity);
        if (FrameManagerUtility.TryGet(EntityManager, out FrameManager frame))
        {
            if (_castOrdinalFrame != frame.currentFrame)
            {
                _castOrdinalFrame = frame.currentFrame;
                _castOrdinals.Clear();
            }
            _castOrdinals.TryGetValue(entity, out int ordinal);
            _castOrdinals[entity] = ordinal + 1;
            request.CastFrame = frame.currentFrame;
            request.CastOrdinal = ordinal;
            request.HasCastIdentity = 1;
        }
        if (!SkillReleaseSnapshotUtility.TryCreate(EntityManager, in request, out ResolvedSkillData resolvedSkill))
        {
            Debug.LogError($"[StateScriptManagedCommand] Failed to analyze SkillId={request.SkillId}.");
            return;
        }

        if (!SkillReleaseUtility.TryExecute(EntityManager, in request, resolvedSkill, _skillContext))
            Debug.LogError($"[StateScriptManagedCommand] Failed to execute SkillId={request.SkillId}.");
    }

    private void RefreshResolver(Entity entity, UnitSourceResolver resolver)
    {
        _sourceDispatcher.Update(this);
        resolver.Update(entity, UnitVariableSource.GetOther(EntityManager, entity), in _sourceDispatcher);
    }

    private UnitSourceResolver GetOrCreateResolver(Entity entity, ref UnitSourceResolver resolver)
    {
        if (resolver != null)
            return resolver;

        using (ResolveSourcesMarker.Auto())
        {
            Entity other = UnitVariableSource.GetOther(EntityManager, entity);
            resolver = new UnitSourceResolver(entity);
            resolver.Update(entity, other, in _sourceDispatcher);
        }

        return resolver;
    }

    private void CollectInteraction(Entity target)
    {
        if (!GameInteractionUtility.TryBegin(
                EntityManager,
                _interactionEntity,
                target,
                out InteractionTransactionElement transaction))
            return;

        if (!EntityManager.HasComponent<UnitInteractableComponent>(target))
        {
            GameInteractionUtility.Complete(
                EntityManager,
                _interactionEntity,
                target,
                InteractionResultCode.InvalidTarget,
                UnitSourceValue.FromBool(false));
            return;
        }

        UnitInteractionData data = EntityManager.GetComponentData<UnitInteractableComponent>(target).Data;
        int amount = math.max(0, data.Amount);
        InteractionResultCode resultCode = InteractionResultCode.Failed;
        if (data.Kind == InteractionKind.Drop && amount > 0)
        {
            DropRewardType dropType = (DropRewardType)data.Variant;
            if (dropType == DropRewardType.Money)
            {
                if (GameWorldContextUtility.GetSceneMode(EntityManager) == GameSceneMode.Dungeon &&
                    GameRuntimeStateUtility.TryGetPlayerCharacterData(EntityManager, transaction.Actor, out CharacterData characterData))
                {
                    characterData.Money = System.Math.Max(0L, characterData.Money + amount);
                    DungeonSettlementUtility.RecordMoneyAcquired(EntityManager, amount);
                    PlayerCharacterUtility.MarkChanged(EntityManager, transaction.Actor);
                    resultCode = InteractionResultCode.Success;
                    PublishPickupFeedback(transaction.Actor, PickupFeedbackType.Money, -1, amount);
                }
                else
                {
                    StashData stashData = GameRuntimeStateUtility.GetStashData();
                    if (stashData != null)
                    {
                        stashData.Money = System.Math.Max(0L, stashData.Money + amount);
                        SaveDataComponent.Instance.NotifyStashDataChanged();
                        resultCode = InteractionResultCode.Success;
                        PublishPickupFeedback(transaction.Actor, PickupFeedbackType.Money, -1, amount);
                    }
                }
            }
            else if (dropType == DropRewardType.Item && data.DataId >= 0 &&
                     GameRuntimeStateUtility.TryGetPlayerCharacterData(EntityManager, transaction.Actor, out CharacterData playerData))
            {
                if (InventoryUtility.CanAddItemToCharacterInventory(
                        playerData.Backpack,
                        playerData.Props,
                        data.DataId,
                        amount) &&
                    InventoryUtility.AddItemToCharacterInventory(
                        playerData.Backpack,
                        playerData.Props,
                        data.DataId,
                        amount) == amount)
                {
                    PlayerCharacterUtility.MarkChanged(EntityManager, transaction.Actor);
                    resultCode = InteractionResultCode.Success;
                    PublishPickupFeedback(transaction.Actor, PickupFeedbackType.Item, data.DataId, amount);
                    DungeonSettlementUtility.RecordItemAcquired(EntityManager, data.DataId, amount);
                }
                else
                {
                    PublishPickupFeedback(transaction.Actor, PickupFeedbackType.BackpackFull, data.DataId, amount);
                }
            }
        }

        GameInteractionUtility.Complete(
            EntityManager,
            _interactionEntity,
            target,
            resultCode,
            UnitSourceValue.FromBool(resultCode == InteractionResultCode.Success));
        if (resultCode == InteractionResultCode.Success)
            MarkForDestroy(target);
    }

    private void PublishPickupFeedback(Entity player, PickupFeedbackType type, int itemId, int amount)
    {
        if (NetworkPresentationEventUtility.TryEnqueuePickupFeedback(EntityManager, player, type, itemId, amount))
            return;

        EventComponent.Instance.Publish(new PickupFeedbackEvent(type, itemId, amount));
    }

    private void StartNpcInteraction(Entity target)
    {
        if (_npcSessions.ContainsKey(target) ||
            !GameInteractionUtility.TryBegin(
                EntityManager,
                _interactionEntity,
                target,
                out InteractionTransactionElement transaction))
            return;

        if (!EntityManager.HasComponent<UnitInteractableComponent>(target))
        {
            GameInteractionUtility.Complete(
                EntityManager,
                _interactionEntity,
                target,
                InteractionResultCode.InvalidTarget,
                UnitSourceValue.FromBool(false));
            return;
        }

        int dataId = EntityManager.GetComponentData<UnitInteractableComponent>(target).Data.DataId;
        NPCData npcData = DataComponent.Instance.Get<NPCData>(dataId);
        NPCInteractionData interaction = SelectNpcInteraction(npcData, transaction);
        if (npcData == null || interaction?.GetEntryNode() == null)
        {
            GameInteractionUtility.Complete(
                EntityManager,
                _interactionEntity,
                target,
                InteractionResultCode.Failed,
                UnitSourceValue.FromBool(false));
            return;
        }

        NPCInteractionSession session = new(target, npcData, interaction, transaction.Actor, World);
        _npcSessions.Add(target, session);
        if (interaction.IsSequence)
            GameInteractionUtility.Complete(EntityManager, _interactionEntity, target,
                InteractionResultCode.Success, UnitSourceValue.FromBool(true));
        RefreshNpcInput();
        EventComponent.Instance.Publish(new NPCInteractionStartedEvent(target, npcData, interaction));
    }

    private static NPCInteractionData SelectNpcInteraction(NPCData npcData, in InteractionTransactionElement transaction)
    {
        if (npcData == null)
            return null;
        foreach (NPCInteractionData interaction in npcData.GetEnabledInteractions(transaction.IsAutomatic != 0))
            if (transaction.InteractionKey.IsEmpty || transaction.InteractionKey.ToString() == interaction.Key)
                return interaction;
        return null;
    }

    private void TickNpcSessions(float deltaTime)
    {
        _completedNpcTargets.Clear();
        _tickingNpcSessions = true;
        try
        {
            foreach (KeyValuePair<Entity, NPCInteractionSession> pair in _npcSessions)
            {
                NPCInteractionSession session = pair.Value;
                if (!session.IsActive) continue;
                if (!session.IsTargetValid(EntityManager) ||
                    !EntityManager.Exists(session.Actor) ||
                    BattlePlayerStatusUtility.IsInputLocked(EntityManager, session.Actor) ||
                    (EntityManager.HasComponent<UnitDeathComponent>(session.Target) &&
                     EntityManager.IsComponentEnabled<UnitDeathComponent>(session.Target)))
                {
                    FinishNpcSession(session, true);
                    continue;
                }

                AdvanceNpcSessionUntilBlocked(session, deltaTime);
            }
        }
        finally
        {
            _tickingNpcSessions = false;
            for (int index = 0; index < _completedNpcTargets.Count; index++)
                _npcSessions.Remove(_completedNpcTargets[index]);
            RefreshNpcInput();
        }
    }

    private void AdvanceNpcSessionUntilBlocked(NPCInteractionSession session, float deltaTime)
    {
        int maxSteps = session.Interaction?.Nodes?.Count + 1 ?? 1;
        for (int index = 0; index < maxSteps; index++)
        {
            NPCInteractionNodeData currentNode = session.GetCurrentNode();
            if (currentNode == null)
            {
                FinishNpcSession(session, false);
                return;
            }

            if (!GameWorldExecutionTargetUtility.Contains(currentNode.ExecutionTargets, _worldRole))
            {
                session.CurrentNodeGuid = ResolveNextNodeGuid(currentNode, null);
                continue;
            }

            if (session.CurrentRunner == null)
            {
                session.CurrentRunner = _npcRunnerFactory.Create(currentNode);
                if (session.CurrentRunner == null)
                {
                    session.CurrentNodeGuid = ResolveNextNodeGuid(currentNode, null);
                    continue;
                }

                EventComponent.Instance.Publish(new NPCInteractionNodeStartedEvent(
                    session.Target,
                    session.NpcData,
                    session.Interaction,
                    currentNode));
                session.SelectedNextNodeGuid = null;
                session.CurrentRunner.Enter(session);
            }

            if (!session.IsActive)
            {
                FinishNpcSession(session, true);
                return;
            }
            session.CurrentRunner.Update(session, deltaTime);
            if (!session.IsActive)
            {
                FinishNpcSession(session, true);
                return;
            }
            if (!session.CurrentRunner.IsCompleted(session))
                return;

            session.CurrentRunner.Exit(session);
            session.CurrentRunner = null;
            session.CurrentNodeGuid = ResolveNextNodeGuid(currentNode, session.SelectedNextNodeGuid);
            session.SelectedNextNodeGuid = null;
        }

        FinishNpcSession(session, true);
    }

    private void FinishNpcSession(NPCInteractionSession session, bool wasCancelled)
    {
        if (!session.TryFinish()) return;
        session.ClearGuide();
        string completionVariable = session.Interaction?.CompletionVariable;
        if (!wasCancelled && !string.IsNullOrWhiteSpace(completionVariable))
        {
            SaveDataComponent save = SaveDataComponent.Instance;
            if (_worldRole != GameWorldRole.Standalone || save == null || !save.ContainsVariable(completionVariable))
                wasCancelled = true;
            else
            {
                double previous = save.GetVariable(completionVariable);
                save.SetVariable(completionVariable, 1d);
                if (!save.Save())
                {
                    save.SetVariable(completionVariable, previous);
                    wasCancelled = true;
                }
            }
        }
        if (wasCancelled)
        {
            session.Cancel();
            if (session.Interaction.IsSequence)
                World.GetExistingSystemManaged<AutoInteractionSystem>()?.DeferRetry(session.Actor, session.Target, session.Interaction.RetrySeconds);
        }
        EventComponent.Instance.Publish(new NPCInteractionFinishedEvent(
            session.Target,
            session.NpcData,
            session.Interaction,
            wasCancelled));
        if (!session.Interaction.IsSequence) GameInteractionUtility.Complete(
            EntityManager,
            _interactionEntity,
            session.Target,
            wasCancelled ? InteractionResultCode.Cancelled : InteractionResultCode.Success,
            UnitSourceValue.FromBool(!wasCancelled));
        _completedNpcTargets.Add(session.Target);
    }

    private static string ResolveNextNodeGuid(NPCInteractionNodeData node, string selectedNextNodeGuid)
    {
        if (!string.IsNullOrWhiteSpace(selectedNextNodeGuid))
            return selectedNextNodeGuid;
        if (node?.Branches == null)
            return null;
        for (int index = 0; index < node.Branches.Count; index++)
        {
            NPCInteractionBranchData branch = node.Branches[index];
            if (branch != null && branch.IsEnabled())
                return branch.NextNodeGuid;
        }
        return null;
    }

    private void AcquireNpcInput()
    {
        if (_npcInputLocked || _worldRole == GameWorldRole.Server)
            return;
        GameGateComponent.Instance.Lock(GameGateType.PlayerInput, NpcSessionInputLockReason);
        _npcInputLocked = true;
    }

    private void RefreshNpcInput()
    {
        foreach (NPCInteractionSession session in _npcSessions.Values)
            if (session.IsActive && (!session.Interaction.IsSequence || session.GetCurrentNode() is NPCDialogueInteractionNodeData or NPCCameraInteractionNodeData))
            {
                AcquireNpcInput();
                return;
            }
        ReleaseNpcInput();
    }

    private void ReleaseNpcInput()
    {
        if (!_npcInputLocked)
            return;
        GameGateComponent.Instance.Unlock(GameGateType.PlayerInput, NpcSessionInputLockReason);
        _npcInputLocked = false;
    }

    private void StartAddition(
        Entity entity,
        int graphIndex,
        int nodeIndex,
        uint executionVersion,
        in StateScriptNodeDefinition node,
        UnitSourceResolver resolver)
    {
        StateScriptActionKey key = new(entity, graphIndex, nodeIndex);
        StopAddition(key);
        List<SkillAdditionAction> actions = SkillAdditionEventDispatcher.CreateActions(
            EntityManager,
            entity,
            resolver,
            node.Text.ToString());
        int createdActionCount = actions.Count;
        RemoveFinishedActions(actions);
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
        Debug.Log(
            $"[StateScriptAdditionTrace] Started: Entity={entity}, GraphIndex={graphIndex}, " +
            $"NodeIndex={nodeIndex}, Node='{node.Guid}', Event='{node.Text}', Version={executionVersion}, " +
            $"CreatedActions={createdActionCount}, RunningActions={actions.Count}.");
#endif
        if (actions.Count == 0)
        {
            AppendExternalCompletion(key, executionVersion);
            return;
        }
        _runningActions.Add(key, new RunningAddition
        {
            ExecutionVersion = executionVersion,
            Actions = actions,
        });
    }

    private void TickRunningActions()
    {
        _completedKeys.Clear();
        foreach (KeyValuePair<StateScriptActionKey, RunningAddition> pair in _runningActions)
        {
            StateScriptActionKey key = pair.Key;
            if (EntityManager.Exists(key.Entity) &&
                !_scope.Includes(EntityManager.HasComponent<PlayerInputComponent>(key.Entity)))
                continue;
            RunningAddition execution = pair.Value;
            if (!EntityManager.Exists(key.Entity) ||
                EntityManager.HasComponent<BattleSpectatorComponent>(key.Entity) ||
                (EntityManager.HasComponent<UnitDeathComponent>(key.Entity) &&
                 EntityManager.IsComponentEnabled<UnitDeathComponent>(key.Entity)) ||
                !EntityManager.HasComponent<UnitStateScriptComponent>(key.Entity))
            {
                StopActions(execution.Actions);
                _completedKeys.Add(key);
                continue;
            }
            List<SkillAdditionAction> actions = execution.Actions;
            for (int actionIndex = actions.Count - 1; actionIndex >= 0; actionIndex--)
            {
                SkillAdditionAction action = actions[actionIndex];
                action?.Tick();
                if (action == null || action.Status != SkillAdditionActionStatus.Running)
                    actions.RemoveAt(actionIndex);
            }
            if (actions.Count == 0)
            {
                AppendExternalCompletion(key, execution.ExecutionVersion);
                _completedKeys.Add(key);
            }
        }
        for (int index = 0; index < _completedKeys.Count; index++)
            _runningActions.Remove(_completedKeys[index]);
    }

    private void AppendExternalCompletion(StateScriptActionKey key, uint executionVersion)
    {
        if (!EntityManager.Exists(key.Entity) ||
            !EntityManager.HasBuffer<StateScriptExternalResultElement>(key.Entity))
        {
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
            Debug.LogWarning(
                $"[StateScriptAdditionTrace] Completion dropped: Entity={key.Entity}, " +
                $"GraphIndex={key.GraphIndex}, NodeIndex={key.NodeIndex}, Version={executionVersion}, " +
                $"EntityExists={EntityManager.Exists(key.Entity)}.");
#endif
            return;
        }
        DynamicBuffer<StateScriptExternalResultElement> results =
            EntityManager.GetBuffer<StateScriptExternalResultElement>(key.Entity);
        results.Add(
            new StateScriptExternalResultElement
            {
                GraphIndex = key.GraphIndex,
                NodeIndex = key.NodeIndex,
                ExecutionVersion = executionVersion,
                Status = StateScriptExternalResultStatus.Completed,
            });
#if UNITY_EDITOR && STATE_SCRIPT_TRACE
        Debug.Log(
            $"[StateScriptAdditionTrace] Completion queued: Entity={key.Entity}, " +
            $"GraphIndex={key.GraphIndex}, NodeIndex={key.NodeIndex}, Version={executionVersion}, " +
            $"PendingResults={results.Length}.");
#endif
    }

    private void StopActions(Entity entity)
    {
        _completedKeys.Clear();
        foreach (KeyValuePair<StateScriptActionKey, RunningAddition> pair in _runningActions)
        {
            if (pair.Key.Entity != entity)
                continue;
            StopActions(pair.Value.Actions);
            _completedKeys.Add(pair.Key);
        }
        for (int index = 0; index < _completedKeys.Count; index++)
            _runningActions.Remove(_completedKeys[index]);
    }

    private void StopAddition(StateScriptActionKey key, uint executionVersion)
    {
        if (!_runningActions.TryGetValue(key, out RunningAddition execution) ||
            execution.ExecutionVersion != executionVersion)
        {
            return;
        }
        StopAddition(key);
    }

    private void StopAddition(StateScriptActionKey key)
    {
        if (!_runningActions.TryGetValue(key, out RunningAddition execution))
            return;
        StopActions(execution.Actions);
        _runningActions.Remove(key);
    }

    private static void StopActions(List<SkillAdditionAction> actions)
    {
        for (int index = 0; index < actions.Count; index++)
            actions[index]?.Stop();
        actions.Clear();
    }

    private static void RemoveFinishedActions(List<SkillAdditionAction> actions)
    {
        for (int index = actions.Count - 1; index >= 0; index--)
        {
            if (actions[index] == null || actions[index].Status != SkillAdditionActionStatus.Running)
                actions.RemoveAt(index);
        }
    }

    private void SpawnUnits(
        Entity spawner,
        int graphIndex,
        int nodeIndex,
        int tickSeed,
        ref StateScriptGraphDefinitionBlob graph,
        in StateScriptNodeDefinition node)
    {
        if (EntityManager.HasComponent<DungeonInterestPointComponent>(spawner) &&
            DungeonPatrolRuntimeUtility.IsEncounterDead(EntityManager, spawner))
        {
            return;
        }

        if (node.Text.Length > 0)
        {
            if (node.SpawnIntervalSeconds > 0f)
            {
                StartSpawnBatch(spawner, graphIndex, nodeIndex, in node);
                return;
            }
            int spawnedCount = SpawnVariableList(spawner, node.Text.ToString(), in node);
            NotificationSignalUtility.PublishSpawned(EntityManager, spawner, node.NotificationKey.ToString(), spawnedCount);
            return;
        }
        if (node.StringCount == 0 || !EntityManager.HasComponent<LocalTransform>(spawner))
            return;

        float3 center = EntityManager.GetComponentData<LocalTransform>(spawner).Position + node.FloatParameters1.xyz;
        uint seed = math.hash(new uint4(
            (uint)math.max(1, spawner.Index),
            (uint)(spawner.Version ^ tickSeed),
            (uint)(graphIndex + 1),
            (uint)(nodeIndex + 1)));
        Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(math.max(1u, seed));
        float maxRadius = node.FloatParameters0.x;
        float minRadius = node.FloatParameters0.y;
        int spawnedTotal = 0;
        for (int index = 0; index < math.max(1, node.IntParameters.x); index++)
        {
            int candidateOffset = node.StringCount == 1 ? 0 : random.NextInt(0, node.StringCount);
            string unitName = graph.Strings[node.StringStart + candidateOffset].ToString();
            if (!SpawnPositionUtility.TrySample(EntityManager, center, minRadius, maxRadius,
                    node.FloatParameters0.z != 0f, node.FloatParameters0.w, (int)node.FloatParameters1.w,
                    ref random, out float3 position)) continue;
            if (TrySpawn(spawner, unitName, position, in node, default, false))
                spawnedTotal++;
        }
        NotificationSignalUtility.PublishSpawned(EntityManager, spawner, node.NotificationKey.ToString(), spawnedTotal);
    }

    private int SpawnVariableList(Entity spawner, string listKey, in StateScriptNodeDefinition node)
    {
        if (!TryGetInt(spawner, $"{listKey}.count", out int count) || count <= 0)
            return 0;
        int spawnedCount = 0;
        for (int index = 0; index < count; index++)
        {
            if (!TryReadSpawnEntry(spawner, listKey, index, out StateScriptSpawnBatch.Entry entry))
                continue;
            if (TrySpawn(spawner, entry.UnitName, entry.Position, in node, entry.Info, entry.HasInfo)) spawnedCount++;
        }
        return spawnedCount;
    }

    private bool TryReadSpawnEntry(Entity spawner, string listKey, int index, out StateScriptSpawnBatch.Entry entry)
    {
        entry = default;
        string entryKey = $"{listKey}.{index}";
        if (!TryGetString(spawner, $"{entryKey}.unit", out entry.UnitName) ||
            !TryGetFloat3(spawner, $"{entryKey}.position", out entry.Position) ||
            !math.all(math.isfinite(entry.Position)))
            return false;

        entry.HasInfo = TryGetBool(spawner, $"{entryKey}.hasMonsterData", out bool hasMonsterData) && hasMonsterData;
        if (entry.HasInfo)
        {
            entry.Info = new NetworkEntitySpawnInfo { hasMonsterSpawnData = true };
            TryGetInt(spawner, $"{entryKey}.monsterSaveId", out entry.Info.monsterSaveId);
            TryGetInt(spawner, $"{entryKey}.monsterRegionId", out entry.Info.monsterRegionId);
            TryGetInt(spawner, $"{entryKey}.monsterSquadId", out entry.Info.monsterSquadId);
            TryGetBool(spawner, $"{entryKey}.monsterIsBoss", out entry.Info.monsterIsBoss);
        }
        return true;
    }

    private void StartSpawnBatch(Entity spawner, int graphIndex, int nodeIndex, in StateScriptNodeDefinition node)
    {
        StateScriptActionKey key = new(spawner, graphIndex, nodeIndex);
        // Repeated pulses must not restart or duplicate an in-flight wave.
        if (_spawnBatches.ContainsKey(key))
            return;
        if (!StateScriptSpawnBatch.CanContinue(EntityManager, spawner, graphIndex))
        {
            SetPendingSpawnCount(spawner, node.Text.ToString(), 0);
            return;
        }
        List<StateScriptSpawnBatch.Entry> entries = new();
        string listKey = node.Text.ToString();
        if (TryGetInt(spawner, listKey + ".count", out int count))
            for (int i = 0; i < count; i++)
                if (TryReadSpawnEntry(spawner, listKey, i, out StateScriptSpawnBatch.Entry entry))
                    entries.Add(entry);

        StateScriptSpawnBatch batch = new(entries, in node);
        _spawnBatches.Add(key, batch);
        AdvanceSpawnBatch(spawner, batch, 0f);
        if (batch.RemainingCount == 0)
        {
            NotificationSignalUtility.PublishSpawned(EntityManager, spawner, node.NotificationKey.ToString(), batch.SpawnedCount);
            _spawnBatches.Remove(key);
        }
    }

    private void TickSpawnBatches(float deltaTime)
    {
        _completedSpawnBatches.Clear();
        foreach (KeyValuePair<StateScriptActionKey, StateScriptSpawnBatch> pair in _spawnBatches)
        {
            Entity spawner = pair.Key.Entity;
            if (EntityManager.Exists(spawner) &&
                !_scope.Includes(EntityManager.HasComponent<PlayerInputComponent>(spawner)))
                continue;
            StateScriptSpawnBatch batch = pair.Value;
            if (!StateScriptSpawnBatch.CanContinue(EntityManager, spawner, pair.Key.GraphIndex))
            {
                SetPendingSpawnCount(spawner, batch.Node.Text.ToString(), 0);
                _completedSpawnBatches.Add(pair.Key);
                continue;
            }
            AdvanceSpawnBatch(spawner, batch, deltaTime);
            if (batch.RemainingCount != 0)
                continue;
            NotificationSignalUtility.PublishSpawned(EntityManager, spawner, batch.Node.NotificationKey.ToString(), batch.SpawnedCount);
            _completedSpawnBatches.Add(pair.Key);
        }
        foreach (StateScriptActionKey key in _completedSpawnBatches)
            _spawnBatches.Remove(key);
    }

    private void AdvanceSpawnBatch(Entity spawner, StateScriptSpawnBatch batch, float deltaTime)
    {
        if (batch.TryTakeNext(deltaTime, out StateScriptSpawnBatch.Entry entry) &&
            TrySpawn(spawner, entry.UnitName, entry.Position, in batch.Node, entry.Info, entry.HasInfo))
            batch.SpawnedCount++;
        // Failed entries are consumed as well: missing prefabs must not block a wave forever.
        SetPendingSpawnCount(spawner, batch.Node.Text.ToString(), batch.RemainingCount);
    }

    private void SetPendingSpawnCount(Entity spawner, string listKey, int count)
    {
        if (EntityManager.Exists(spawner))
            UnitVariableSource.TrySetValue(EntityManager, spawner, listKey + ".pendingCount", UnitValue.FromInt(count));
    }

    private bool TrySpawn(
        Entity spawner,
        string unitName,
        float3 position,
        in StateScriptNodeDefinition node,
        NetworkEntitySpawnInfo additionalInfo,
        bool hasAdditionalInfo)
    {
        if (node.FloatParameters0.z != 0f && !SpawnPositionUtility.IsValid(EntityManager, position, node.FloatParameters0.w))
            return false;
        NetworkEntitySpawnInfo info = NetworkEntitySpawnUtility.CreateInfo(
            NetworkEntityPrefabType.Unit,
            unitName,
            new Vector3(position.x, position.y, position.z));
        if (hasAdditionalInfo)
        {
            info.hasMonsterSpawnData = additionalInfo.hasMonsterSpawnData;
            info.monsterSaveId = additionalInfo.monsterSaveId;
            info.monsterRegionId = additionalInfo.monsterRegionId;
            info.monsterSquadId = additionalInfo.monsterSquadId;
            info.monsterIsBoss = additionalInfo.monsterIsBoss;
        }
        if (node.IntParameters.y != 0 && EntityManager.HasComponent<UnitFactionComponent>(spawner))
        {
            info.hasFaction = true;
            info.faction = EntityManager.GetComponentData<UnitFactionComponent>(spawner).Value;
        }
        if (!NetworkEntitySpawnUtility.TrySpawn(EntityManager, info, out Entity spawned))
            return false;
        DungeonDifficultyUtility.Inherit(EntityManager, spawner, spawned);
        if (EntityManager.HasComponent<DungeonRuntimeOwnedEntity>(spawner) &&
            !EntityManager.HasComponent<DungeonRuntimeOwnedEntity>(spawned))
            EntityManager.AddComponent<DungeonRuntimeOwnedEntity>(spawned);
        if (node.Key.Length > 0)
        {
            if (!EntityManager.HasComponent<UnitVariableComponent>(spawned))
                EntityManager.AddComponent<UnitVariableComponent>(spawned);
            if (!EntityManager.HasBuffer<UnitVariableElement>(spawned))
                EntityManager.AddBuffer<UnitVariableElement>(spawned);
            UnitVariableSource.TrySetValue(EntityManager, spawned, node.Key.ToString(), UnitValue.FromBool(true));
        }
        if (node.IntParameters.z != 0)
        {
            if (!EntityManager.HasComponent<UnitVariableComponent>(spawned))
                EntityManager.AddComponentData(spawned, new UnitVariableComponent { Other = Entity.Null });
            if (!EntityManager.HasBuffer<UnitVariableElement>(spawned))
                EntityManager.AddBuffer<UnitVariableElement>(spawned);
            if (!EntityManager.HasBuffer<UnitVariableConsumerElement>(spawned))
                EntityManager.AddBuffer<UnitVariableConsumerElement>(spawned);
            UnitVariableSource.SetOther(EntityManager, spawned, spawner);

            UnitOwnerComponent owner = new() { Owner = spawner };
            if (EntityManager.HasComponent<UnitOwnerComponent>(spawned))
                EntityManager.SetComponentData(spawned, owner);
            else
                EntityManager.AddComponentData(spawned, owner);
        }

        if (node.IntParameters.z == 0 && node.IntParameters.w == 0 &&
            !EntityManager.HasComponent<DungeonDifficultyComponent>(spawned))
            return true;

        UnitSpawnInitializationComponent initialization = new()
        {
            RestoreRuntimeState = (byte)(node.IntParameters.w != 0 ? 1 : 0),
        };
        if (EntityManager.HasComponent<UnitSpawnInitializationComponent>(spawned))
            EntityManager.SetComponentData(spawned, initialization);
        else
            EntityManager.AddComponentData(spawned, initialization);
        return true;
    }

    private bool TryGetValue(Entity entity, string key, out UnitSourceValue value)
    {
        return UnitVariableSource.TryGetValue(EntityManager, entity, key, out value);
    }

    private bool TryGetInt(Entity entity, string key, out int value)
    {
        value = 0;
        return TryGetValue(entity, key, out UnitSourceValue source) &&
               source.TryGetNumber(out float number) && math.isfinite(number) &&
               math.abs(number - math.round(number)) <= 0.0001f &&
               (value = (int)math.round(number)) >= 0;
    }

    private bool TryGetString(Entity entity, string key, out string value)
    {
        value = string.Empty;
        if (!TryGetValue(entity, key, out UnitSourceValue source) ||
            !source.TryGetString(out FixedString128Bytes fixedValue))
            return false;
        value = fixedValue.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private bool TryGetFloat3(Entity entity, string key, out float3 value)
    {
        value = float3.zero;
        return TryGetValue(entity, key, out UnitSourceValue source) && source.TryGetFloat3(out value);
    }

    private bool TryGetBool(Entity entity, string key, out bool value)
    {
        value = false;
        if (!TryGetValue(entity, key, out UnitSourceValue source) || source.Type != UnitValueType.Bool)
            return false;
        value = source.Bool != 0;
        return true;
    }
}

internal readonly struct StateScriptEffectKey : System.IEquatable<StateScriptEffectKey>
{
    public StateScriptEffectKey(int unitDataId, int graphIndex, int nodeIndex)
    {
        UnitDataId = unitDataId;
        GraphIndex = graphIndex;
        NodeIndex = nodeIndex;
    }

    private int UnitDataId { get; }
    private int GraphIndex { get; }
    private int NodeIndex { get; }

    public bool Equals(StateScriptEffectKey other)
    {
        return UnitDataId == other.UnitDataId &&
               GraphIndex == other.GraphIndex &&
               NodeIndex == other.NodeIndex;
    }

    public override bool Equals(object obj)
    {
        return obj is StateScriptEffectKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = UnitDataId;
            hash = hash * 397 ^ GraphIndex;
            return hash * 397 ^ NodeIndex;
        }
    }
}
