using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using CrystalMagic.Game.Unit;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(StateScriptSystem))]
public partial class StateScriptManagedCommandSystem : SystemBase
{
    private readonly Dictionary<StateScriptActionKey, List<SkillAdditionAction>> _runningActions = new();
    private readonly List<StateScriptActionKey> _completedKeys = new();
    private UnitSourceDispatcher _sourceDispatcher;
    private EntityQuery _commandQuery;

    protected override void OnCreate()
    {
        _sourceDispatcher.Initialize(this);
        _commandQuery = GetEntityQuery(
            ComponentType.ReadOnly<UnitStateScriptComponent>(),
            ComponentType.ReadWrite<StateScriptManagedCommandElement>());
        RequireForUpdate<StateScriptRuntimeRegistryComponent>();
    }

    protected override void OnUpdate()
    {
        Dependency.Complete();
        BlobAssetReference<StateScriptRuntimeRegistryBlob> registry =
            SystemAPI.GetSingleton<StateScriptRuntimeRegistryComponent>().Value;
        if (!registry.IsCreated)
            return;

        _sourceDispatcher.Update(this);
        TickRunningActions();
        using NativeArray<Entity> entities = _commandQuery.ToEntityArray(Allocator.Temp);
        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            Entity entity = entities[entityIndex];
            UnitStateScriptComponent component = EntityManager.GetComponentData<UnitStateScriptComponent>(entity);
            DynamicBuffer<StateScriptManagedCommandElement> commands =
                EntityManager.GetBuffer<StateScriptManagedCommandElement>(entity);
            if (component.DefinitionIndex < 0 ||
                component.DefinitionIndex >= registry.Value.Units.Length)
            {
                commands.Clear();
                continue;
            }

            if (component.IsStoppedForDeath != 0 || EntityManager.HasComponent<UnitDeathComponent>(entity))
            {
                StopActions(entity);
                commands.Clear();
                continue;
            }

            ref StateScriptUnitDefinitionBlob unit = ref registry.Value.Units[component.DefinitionIndex];
            Entity other = UnitVariableSource.GetOther(EntityManager, entity);
            UnitSourceResolver resolver = new(entity);
            resolver.Update(entity, other, in _sourceDispatcher);
            using NativeArray<StateScriptManagedCommandElement> pendingCommands =
                commands.ToNativeArray(Allocator.Temp);
            commands.Clear();
            for (int commandIndex = 0; commandIndex < pendingCommands.Length; commandIndex++)
            {
                StateScriptManagedCommandElement command = pendingCommands[commandIndex];
                if ((uint)command.GraphIndex >= (uint)unit.Graphs.Length)
                    continue;
                ref StateScriptGraphDefinitionBlob graph = ref unit.Graphs[command.GraphIndex];
                if ((uint)command.NodeIndex >= (uint)graph.Nodes.Length)
                    continue;
                ref StateScriptNodeDefinition node = ref graph.Nodes[command.NodeIndex];
                ExecuteCommand(entity, command, ref graph, ref node, resolver);
            }
        }
    }

    protected override void OnDestroy()
    {
        foreach (KeyValuePair<StateScriptActionKey, List<SkillAdditionAction>> pair in _runningActions)
            StopActions(pair.Value);
        _runningActions.Clear();
    }

    private void ExecuteCommand(
        Entity entity,
        StateScriptManagedCommandElement command,
        ref StateScriptGraphDefinitionBlob graph,
        ref StateScriptNodeDefinition node,
        UnitSourceResolver resolver)
    {
        switch (command.Type)
        {
            case StateScriptManagedCommandType.RequestSkill:
                AppendSkillRequest(entity, command, false);
                break;
            case StateScriptManagedCommandType.RequestSkillWithAddition:
                AppendSkillRequest(entity, command, true);
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
                SubmitInteraction(entity, command, in node);
                break;
            case StateScriptManagedCommandType.SpawnUnit:
                SpawnUnits(entity, command.GraphIndex, command.NodeIndex, command.IntValue, ref graph, in node);
                RefreshResolver(entity, resolver);
                break;
            case StateScriptManagedCommandType.StartAddition:
                StartAddition(entity, command.GraphIndex, command.NodeIndex, in node, resolver);
                RefreshResolver(entity, resolver);
                break;
            case StateScriptManagedCommandType.StopAddition:
                StopAddition(new StateScriptActionKey(entity, command.GraphIndex, command.NodeIndex));
                break;
        }
    }

    private void AppendSkillRequest(Entity entity, StateScriptManagedCommandElement command, bool withAddition)
    {
        if (!EntityManager.HasComponent<UnitSkillReleaseComponent>(entity) ||
            !EntityManager.HasBuffer<SkillReleaseRequest>(entity))
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
        EntityManager.GetBuffer<SkillReleaseRequest>(entity).Add(request);
    }

    private void RefreshResolver(Entity entity, UnitSourceResolver resolver)
    {
        _sourceDispatcher.Update(this);
        resolver.Update(entity, UnitVariableSource.GetOther(EntityManager, entity), in _sourceDispatcher);
    }

    private void SubmitInteraction(
        Entity entity,
        StateScriptManagedCommandElement command,
        in StateScriptNodeDefinition node)
    {
        InteractionRequestSnapshot interaction;
        if ((InteractionRequestSource)node.IntParameters.x == InteractionRequestSource.Getter)
        {
            if (!_sourceDispatcher.TryGetInteraction(out interaction))
                return;
        }
        else
        {
            interaction = new InteractionRequestSnapshot
            {
                Target = command.TargetEntity,
                Data = node.InteractionData,
            };
        }
        GameInteractionRequestUtility.TrySubmit(EntityManager, entity, interaction);
    }

    private void StartAddition(
        Entity entity,
        int graphIndex,
        int nodeIndex,
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
        RemoveFinishedActions(actions);
        if (actions.Count == 0)
        {
            AppendExternalCompletion(key);
            return;
        }
        _runningActions.Add(key, actions);
    }

    private void TickRunningActions()
    {
        _completedKeys.Clear();
        foreach (KeyValuePair<StateScriptActionKey, List<SkillAdditionAction>> pair in _runningActions)
        {
            StateScriptActionKey key = pair.Key;
            if (!EntityManager.Exists(key.Entity) ||
                EntityManager.HasComponent<UnitDeathComponent>(key.Entity) ||
                !EntityManager.HasComponent<UnitStateScriptComponent>(key.Entity))
            {
                StopActions(pair.Value);
                _completedKeys.Add(key);
                continue;
            }
            List<SkillAdditionAction> actions = pair.Value;
            for (int actionIndex = actions.Count - 1; actionIndex >= 0; actionIndex--)
            {
                SkillAdditionAction action = actions[actionIndex];
                action?.Tick();
                if (action == null || action.Status != SkillAdditionActionStatus.Running)
                    actions.RemoveAt(actionIndex);
            }
            if (actions.Count == 0)
            {
                AppendExternalCompletion(key);
                _completedKeys.Add(key);
            }
        }
        for (int index = 0; index < _completedKeys.Count; index++)
            _runningActions.Remove(_completedKeys[index]);
    }

    private void AppendExternalCompletion(StateScriptActionKey key)
    {
        if (!EntityManager.Exists(key.Entity) ||
            !EntityManager.HasBuffer<StateScriptExternalResultElement>(key.Entity))
            return;
        EntityManager.GetBuffer<StateScriptExternalResultElement>(key.Entity).Add(
            new StateScriptExternalResultElement
            {
                GraphIndex = key.GraphIndex,
                NodeIndex = key.NodeIndex,
                Status = StateScriptExternalResultStatus.Completed,
            });
    }

    private void StopActions(Entity entity)
    {
        _completedKeys.Clear();
        foreach (KeyValuePair<StateScriptActionKey, List<SkillAdditionAction>> pair in _runningActions)
        {
            if (pair.Key.Entity != entity)
                continue;
            StopActions(pair.Value);
            _completedKeys.Add(pair.Key);
        }
        for (int index = 0; index < _completedKeys.Count; index++)
            _runningActions.Remove(_completedKeys[index]);
    }

    private void StopAddition(StateScriptActionKey key)
    {
        if (!_runningActions.TryGetValue(key, out List<SkillAdditionAction> actions))
            return;
        StopActions(actions);
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
        if (node.Text.Length > 0)
        {
            SpawnVariableList(spawner, node.Text.ToString(), in node);
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
        for (int index = 0; index < math.max(1, node.IntParameters.x); index++)
        {
            int candidateOffset = node.StringCount == 1 ? 0 : random.NextInt(0, node.StringCount);
            string unitName = graph.Strings[node.StringStart + candidateOffset].ToString();
            float2 direction = math.normalizesafe(random.NextFloat2Direction(), new float2(1f, 0f));
            float radius = math.sqrt(random.NextFloat(minRadius * minRadius, maxRadius * maxRadius));
            TrySpawn(spawner, unitName, center + new float3(direction * radius, 0f), in node, default, false);
        }
    }

    private void SpawnVariableList(Entity spawner, string listKey, in StateScriptNodeDefinition node)
    {
        if (!TryGetInt(spawner, $"{listKey}.count", out int count) || count <= 0)
            return;
        for (int index = 0; index < count; index++)
        {
            string entryKey = $"{listKey}.{index}";
            if (!TryGetString(spawner, $"{entryKey}.unit", out string unitName) ||
                !TryGetFloat3(spawner, $"{entryKey}.position", out float3 position))
                continue;
            NetworkEntitySpawnInfo info = default;
            bool hasInfo = TryGetBool(spawner, $"{entryKey}.hasMonsterData", out bool hasMonsterData) && hasMonsterData;
            if (hasInfo)
            {
                info.hasMonsterSpawnData = true;
                TryGetInt(spawner, $"{entryKey}.monsterSaveId", out info.monsterSaveId);
                TryGetInt(spawner, $"{entryKey}.monsterRegionId", out info.monsterRegionId);
                TryGetInt(spawner, $"{entryKey}.monsterSquadId", out info.monsterSquadId);
                TryGetBool(spawner, $"{entryKey}.monsterIsBoss", out info.monsterIsBoss);
            }
            TrySpawn(spawner, unitName, position, in node, info, hasInfo);
        }
    }

    private void TrySpawn(
        Entity spawner,
        string unitName,
        float3 position,
        in StateScriptNodeDefinition node,
        NetworkEntitySpawnInfo additionalInfo,
        bool hasAdditionalInfo)
    {
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
            return;
        if (node.IntParameters.z != 0)
        {
            if (!EntityManager.HasComponent<UnitVariableComponent>(spawned))
                EntityManager.AddComponentData(spawned, new UnitVariableComponent { Other = Entity.Null });
            if (!EntityManager.HasBuffer<UnitVariableElement>(spawned))
                EntityManager.AddBuffer<UnitVariableElement>(spawned);
            if (!EntityManager.HasBuffer<UnitVariableConsumerElement>(spawned))
                EntityManager.AddBuffer<UnitVariableConsumerElement>(spawned);
            UnitVariableSource.SetOther(EntityManager, spawned, spawner);
        }
        if (node.IntParameters.w != 0)
            GameRuntimeStateUtility.TryRestoreDungeonUnit(EntityManager, spawned);
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
