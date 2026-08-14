using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using CrystalMagic.Game.Unit;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public enum TrainingDebugCommandType
{
    SpawnUnit,
    ClearAI,
    ClearStateTransitions,
    ForceState,
    SetFacing,
    CastSkill,
}

public readonly struct TrainingDebugCommand
{
    public TrainingDebugCommand(TrainingDebugCommandType type, Entity target, string argument)
    {
        Type = type;
        Target = target;
        Argument = argument;
    }

    public TrainingDebugCommandType Type { get; }
    public Entity Target { get; }
    public string Argument { get; }
}

public static class TrainingDebugCommandQueue
{
    private static readonly Queue<TrainingDebugCommand> Commands = new();

    public static string LastResult { get; private set; } = "Ready";
    public static int ResultVersion { get; private set; }

    public static void Enqueue(TrainingDebugCommand command)
    {
        Commands.Enqueue(command);
        SetResult("Debug command queued.");
    }

    public static bool TryDequeue(out TrainingDebugCommand command)
    {
        if (Commands.Count == 0)
        {
            command = default;
            return false;
        }

        command = Commands.Dequeue();
        return true;
    }

    public static void SetResult(string message)
    {
        LastResult = message ?? string.Empty;
        ResultVersion++;
    }

    public static void Clear()
    {
        Commands.Clear();
    }
}

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateBefore(typeof(UnitSkillExecuteSystem))]
partial class TrainingDebugCommandSystem : SystemBase
{
    private const int MaxCommandsPerFrame = 16;
    private const float DebugCastTargetDistance = 12f;

    private EntityQuery _playerQuery;

    protected override void OnCreate()
    {
        _playerQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>(),
            },
            None = new[]
            {
                ComponentType.ReadOnly<Prefab>(),
            },
        });
    }

    protected override void OnDestroy()
    {
        TrainingDebugCommandQueue.Clear();
    }

    protected override void OnUpdate()
    {
        for (int i = 0; i < MaxCommandsPerFrame && TrainingDebugCommandQueue.TryDequeue(out TrainingDebugCommand command); i++)
        {
            Execute(command);
        }
    }

    private void Execute(TrainingDebugCommand command)
    {
        switch (command.Type)
        {
            case TrainingDebugCommandType.SpawnUnit:
                SpawnUnit(command.Argument);
                break;
            case TrainingDebugCommandType.ClearAI:
                ClearAI(command.Target);
                break;
            case TrainingDebugCommandType.ClearStateTransitions:
                ClearStateTransitions(command.Target);
                break;
            case TrainingDebugCommandType.ForceState:
                ForceState(command.Target, command.Argument);
                break;
            case TrainingDebugCommandType.SetFacing:
                SetFacing(command.Target, command.Argument);
                break;
            case TrainingDebugCommandType.CastSkill:
                CastSkill(command.Target, command.Argument);
                break;
        }
    }

    private void SpawnUnit(string unitName)
    {
        if (string.IsNullOrWhiteSpace(unitName))
        {
            TrainingDebugCommandQueue.SetResult("Enter a unit prefab name.");
            return;
        }

        if (!TryGetPlayerPosition(out float3 playerPosition))
        {
            TrainingDebugCommandQueue.SetResult("Cannot find an active player spawn position.");
            return;
        }

        FixedString128Bytes prefabName = new FixedString128Bytes(unitName.Trim());
        if (!EntitySpawnRegistryUtility.TryInstantiateUnit(EntityManager, prefabName, out Entity unitEntity))
        {
            TrainingDebugCommandQueue.SetResult($"Unit prefab '{unitName}' is not registered in this scene.");
            return;
        }

        float3 spawnPosition = playerPosition + new float3(1.5f, 0f, 0f);
        if (EntityManager.HasComponent<LocalTransform>(unitEntity))
        {
            LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(unitEntity);
            transform.Position = spawnPosition;
            EntityManager.SetComponentData(unitEntity, transform);
        }
        else
        {
            EntityManager.AddComponentData(unitEntity, LocalTransform.FromPosition(spawnPosition));
        }

        TrainingDebugCommandQueue.SetResult($"Spawned '{unitName}' as {unitEntity}.");
    }

    private void ClearAI(Entity entity)
    {
        if (!IsValidUnit(entity))
            return;

        if (EntityManager.HasComponent<UnitBehaviorTreeComponent>(entity))
            EntityManager.RemoveComponent<UnitBehaviorTreeComponent>(entity);

        // Perception alone does not control a unit; retaining it lets the debug caster keep a target.
        EnsurePerception(entity);

        if (EntityManager.HasComponent<UnitIntentComponent>(entity))
        {
            UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(entity);
            intent.ClearFrameIntent();
            EntityManager.SetComponentData(entity, intent);
        }

        if (EntityManager.HasComponent<UnitMoveComponent>(entity))
        {
            UnitMoveComponent move = EntityManager.GetComponentData<UnitMoveComponent>(entity);
            move.ClearTargetMovement();
            move.Velocity = float2.zero;
            EntityManager.SetComponentData(entity, move);
        }

        if (EntityManager.HasComponent<UnitSkillComponent>(entity))
        {
            UnitSkillComponent skill = EntityManager.GetComponentData<UnitSkillComponent>(entity);
            skill.ClearPending();
            EntityManager.SetComponentData(entity, skill);
        }

        TrainingDebugCommandQueue.SetResult($"AI removed from {entity}.");
    }

    private void ClearStateTransitions(Entity entity)
    {
        if (!TryGetStateMachine(entity, out UnitStateMachineComponent stateMachine))
            return;

        int clearedCount = ClearAllTransitions(stateMachine);
        TrainingDebugCommandQueue.SetResult($"Cleared {clearedCount} state transition rule(s) on {entity}.");
    }

    private void ForceState(Entity entity, string stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName))
        {
            TrainingDebugCommandQueue.SetResult("Enter a state class name.");
            return;
        }

        if (!TryGetStateMachine(entity, out UnitStateMachineComponent stateMachine))
            return;

        ClearAllTransitions(stateMachine);
        string trimmedStateName = stateName.Trim();
        if (!UnitStateMachineUtility.TryForceState(stateMachine, trimmedStateName))
        {
            TrainingDebugCommandQueue.SetResult($"State '{trimmedStateName}' does not exist on {entity}.");
            return;
        }

        TrainingDebugCommandQueue.SetResult($"Forced {entity} into {trimmedStateName}; all transitions are cleared.");
    }

    private void SetFacing(Entity entity, string directionName)
    {
        if (!IsValidUnit(entity))
            return;

        if (!EntityManager.HasComponent<UnitFacingComponent>(entity))
        {
            TrainingDebugCommandQueue.SetResult($"{entity} has no facing component.");
            return;
        }

        if (!TryParseFacing(directionName, out float2 direction))
        {
            TrainingDebugCommandQueue.SetResult("Facing must be Left, Right, Up, or Down.");
            return;
        }

        UnitFacingUtility.SetFacing(EntityManager, entity, direction);
        SetDebugCastTargetFromFacing(entity);
        TrainingDebugCommandQueue.SetResult($"Set {entity} facing to {directionName}.");
    }

    private void CastSkill(Entity entity, string skillIdText)
    {
        if (!TryGetStateMachine(entity, out UnitStateMachineComponent stateMachine))
            return;

        if (!int.TryParse(skillIdText, out int skillId))
        {
            TrainingDebugCommandQueue.SetResult("Enter a numeric SkillId.");
            return;
        }

        if (!EntityManager.HasComponent<UnitSkillComponent>(entity) ||
            !EntityManager.HasComponent<UnitCastComponent>(entity))
        {
            TrainingDebugCommandQueue.SetResult($"{entity} cannot cast skills.");
            return;
        }

        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(entity);
        UnitSkillComponent unitSkill = EntityManager.GetComponentData<UnitSkillComponent>(entity);
        int skillIndex = FindSkillIndex(unitSkill, skillId);
        if (skillIndex < 0)
        {
            TrainingDebugCommandQueue.SetResult($"{entity} does not have SkillId {skillId}.");
            return;
        }

        if (!SkillAnalysisUtility.TryAnalyzeSkill(EntityManager, entity, skillId, -1, out ResolvedSkillData skillData))
        {
            TrainingDebugCommandQueue.SetResult($"SkillId {skillId} could not be analyzed for {entity}.");
            return;
        }

        if (EntityManager.HasComponent<UnitManaComponent>(entity))
        {
            UnitManaComponent mana = EntityManager.GetComponentData<UnitManaComponent>(entity);
            if (mana.CurrentMana < skillData.MpCost)
            {
                TrainingDebugCommandQueue.SetResult($"{entity} needs {skillData.MpCost} MP to cast SkillId {skillId}.");
                return;
            }
        }

        if (!EnsureCastTarget(entity))
            return;

        if (cast.IsCasting || cast.HasPreparedCast)
        {
            SkillExecutionUtility.ResetCastState(EntityManager, entity, ref cast);
            unitSkill.ClearPending();
        }

        if (!SkillExecutionUtility.PrepareCast(EntityManager, entity, ref cast, skillId, -1, skillData))
        {
            EntityManager.SetComponentData(entity, unitSkill);
            EntityManager.SetComponentData(entity, cast);
            TrainingDebugCommandQueue.SetResult($"Failed to prepare SkillId {skillId} for {entity}.");
            return;
        }

        EntityManager.SetComponentData(entity, unitSkill);
        EntityManager.SetComponentData(entity, cast);
        if (!UnitStateMachineUtility.TryForceState(stateMachine, "UnitCastState"))
        {
            SkillExecutionUtility.ResetCastState(EntityManager, entity, ref cast);
            EntityManager.SetComponentData(entity, cast);
            TrainingDebugCommandQueue.SetResult($"{entity} has no UnitCastState.");
            return;
        }

        if (!SkillExecutionUtility.TryStartPreparedSkill(EntityManager, entity, ref cast, out _))
        {
            SkillExecutionUtility.ResetCastState(EntityManager, entity, ref cast);
            EntityManager.SetComponentData(entity, cast);
            TrainingDebugCommandQueue.SetResult($"Failed to start SkillId {skillId} for {entity}.");
            return;
        }

        UnitSkillEntry entry = unitSkill.Skills[skillIndex];
        entry.CooldownRemaining = math.max(0f, entry.CooldownSeconds);
        unitSkill.Skills[skillIndex] = entry;
        EntityManager.SetComponentData(entity, unitSkill);
        EntityManager.SetComponentData(entity, cast);
        TrainingDebugCommandQueue.SetResult($"Started SkillId {skillId} on {entity}.");
    }

    private bool TryGetPlayerPosition(out float3 position)
    {
        if (_playerQuery.IsEmptyIgnoreFilter)
        {
            position = float3.zero;
            return false;
        }

        Entity playerEntity = _playerQuery.GetSingletonEntity();
        position = EntityManager.GetComponentData<LocalTransform>(playerEntity).Position;
        return true;
    }

    private void EnsurePerception(Entity entity)
    {
        if (EntityManager.HasComponent<UnitPerceptionComponent>(entity))
            return;

        EntityManager.AddComponentData(entity, new UnitPerceptionComponent
        {
            SearchRadius = 8f,
            HasTarget = false,
            TargetEntity = Entity.Null,
            TargetPosition = float2.zero,
            TargetDistance = 0f,
        });
    }

    private bool EnsureCastTarget(Entity entity)
    {
        EnsurePerception(entity);
        if (_playerQuery.IsEmptyIgnoreFilter)
        {
            TrainingDebugCommandQueue.SetResult("Cannot find a player target for the debug cast.");
            return false;
        }

        UnitPerceptionComponent perception = EntityManager.GetComponentData<UnitPerceptionComponent>(entity);
        Entity player = _playerQuery.GetSingletonEntity();
        float3 playerPosition = EntityManager.GetComponentData<LocalTransform>(player).Position;
        perception.HasTarget = true;
        perception.TargetEntity = player;
        perception.TargetPosition = playerPosition.xy;
        perception.TargetDistance = EntityManager.HasComponent<LocalTransform>(entity)
            ? math.distance(EntityManager.GetComponentData<LocalTransform>(entity).Position.xy, playerPosition.xy)
            : 0f;
        EntityManager.SetComponentData(entity, perception);
        return true;
    }

    private void SetDebugCastTargetFromFacing(Entity entity)
    {
        if (!EntityManager.HasComponent<UnitIntentComponent>(entity) ||
            !EntityManager.HasComponent<LocalTransform>(entity) ||
            !UnitFacingUtility.TryGetFacing(EntityManager, entity, out float2 facing))
        {
            return;
        }

        UnitIntentComponent intent = EntityManager.GetComponentData<UnitIntentComponent>(entity);
        float2 origin = EntityManager.GetComponentData<LocalTransform>(entity).Position.xy;
        intent.CastTargetPosition = origin + facing * DebugCastTargetDistance;
        EntityManager.SetComponentData(entity, intent);
    }

    private static int FindSkillIndex(in UnitSkillComponent unitSkill, int skillId)
    {
        for (int i = 0; i < unitSkill.Skills.Length; i++)
        {
            if (unitSkill.Skills[i].SkillId == skillId)
                return i;
        }

        return -1;
    }

    private static bool TryParseFacing(string directionName, out float2 direction)
    {
        switch (directionName?.Trim().ToLowerInvariant())
        {
            case "left":
                direction = new float2(-1f, 0f);
                return true;
            case "right":
                direction = new float2(1f, 0f);
                return true;
            case "up":
                direction = new float2(0f, 1f);
                return true;
            case "down":
                direction = new float2(0f, -1f);
                return true;
            default:
                direction = float2.zero;
                return false;
        }
    }

    private bool IsValidUnit(Entity entity)
    {
        if (entity != Entity.Null && EntityManager.Exists(entity) && EntityManager.HasComponent<UnitStateMachineComponent>(entity))
            return true;

        TrainingDebugCommandQueue.SetResult("Select a live non-player unit first.");
        return false;
    }

    private bool TryGetStateMachine(Entity entity, out UnitStateMachineComponent stateMachine)
    {
        if (IsValidUnit(entity))
        {
            stateMachine = EntityManager.GetComponentObject<UnitStateMachineComponent>(entity);
            if (stateMachine != null && stateMachine.StateInstances != null)
                return true;
        }

        TrainingDebugCommandQueue.SetResult("The selected unit state machine is not initialized yet.");
        stateMachine = null;
        return false;
    }

    private static int ClearAllTransitions(UnitStateMachineComponent stateMachine)
    {
        int clearedCount = 0;
        foreach (AUnitState state in stateMachine.StateInstances.Values)
        {
            if (state?.transitions == null)
                continue;

            clearedCount += state.transitions.Count;
            state.transitions.Clear();
        }

        return clearedCount;
    }
}
