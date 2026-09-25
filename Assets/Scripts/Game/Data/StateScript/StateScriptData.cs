using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data.Effects;
using Newtonsoft.Json;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    public sealed class StateScriptData : DataRow
    {
        public List<StateScriptInstanceData> Graphs = new();

        public void EnsureValid()
        {
            Graphs ??= new List<StateScriptInstanceData>();
            for (int i = 0; i < Graphs.Count; i++)
                Graphs[i]?.EnsureValid();
        }
    }

    [Serializable]
    public sealed class StateScriptInstanceData
    {
        public string Guid;
        public string Name;
        // All conditions must pass for this graph to tick. An empty list keeps it active.
        public List<ConditionConfig> ExecutionConditions = new();
        public string EntryNodeGuid;
        public List<StateScriptNodeData> Nodes = new();
        public List<StateScriptEdgeData> Edges = new();
        public Vector2 ViewPosition;
        public float ViewScale = 1f;

        public void EnsureValid()
        {
            Guid ??= System.Guid.NewGuid().ToString("N");
            Name ??= string.Empty;
            ExecutionConditions ??= new List<ConditionConfig>();
            for (int i = 0; i < ExecutionConditions.Count; i++)
            {
                if (ExecutionConditions[i] != null)
                    ExecutionConditions[i].ConditionType = ConditionType.Necessary;
            }
            Nodes ??= new List<StateScriptNodeData>();
            Edges ??= new List<StateScriptEdgeData>();
            ViewScale = Mathf.Max(0.1f, ViewScale);

            for (int i = 0; i < Nodes.Count; i++)
            {
                switch (Nodes[i])
                {
                    case SetValueStateScriptNodeData setValue:
                        setValue.Value ??= new ValueExpression();
                        break;
                    case CompareStateScriptNodeData compare:
                        compare.Condition = EnsureNecessaryCondition(compare.Condition);
                        break;
                    case MonitorStateScriptNodeData monitor:
                        monitor.Condition = EnsureNecessaryCondition(monitor.Condition);
                        break;
                    case RequestSkillActionNodeData requestSkill:
                        requestSkill.SkillId ??= RequestSkillActionNodeData.CreateDefaultSkillIdExpression();
                        requestSkill.Input ??= SkillRequestInputData.CreateDefault();
                        requestSkill.Input.EnsureValid();
                        break;
                    case RequestSkillWithAdditionActionNodeData requestSkillWithAddition:
                        requestSkillWithAddition.SkillId ??= RequestSkillWithAdditionActionNodeData.CreateDefaultSkillIdExpression();
                        requestSkillWithAddition.Input ??= SkillRequestInputData.CreateDefault();
                        requestSkillWithAddition.Input.EnsureValid();
                        break;
                    case RequestInteractionActionNodeData requestInteraction:
                        requestInteraction.Target ??= RequestInteractionActionNodeData.CreateDefaultTargetExpression();
                        break;
                    case CompleteInteractionActionNodeData completeInteraction:
                        completeInteraction.Result ??= CompleteInteractionActionNodeData.CreateDefaultResultExpression();
                        break;
                    case PublishGameEventStateScriptNodeData publishGameEvent:
                        publishGameEvent.Reference ??= PublishGameEventStateScriptNodeData.CreateDefaultReferenceExpression();
                        break;
                    case SpawnUnitActionNodeData spawnUnit:
                        spawnUnit.CandidateUnitNames ??= Array.Empty<string>();
                        spawnUnit.Count = math.max(1, spawnUnit.Count);
                        spawnUnit.SpawnRadius = math.max(0f, spawnUnit.SpawnRadius);
                        spawnUnit.MinSpawnRadius = math.clamp(spawnUnit.MinSpawnRadius, 0f, spawnUnit.SpawnRadius);
                        break;
                    case QueryUnitsActionNodeData queryUnits:
                        queryUnits.EnsureValid();
                        break;
                    case ExecuteEffectActionNodeData executeEffect:
                        executeEffect.EnsureValid();
                        break;
                    case TimerStateScriptNodeData timer:
                        timer.Duration ??= TimerStateScriptNodeData.CreateDefaultDurationExpression();
                        break;
                    case NumberMonitorStateScriptNodeData numberMonitor:
                        numberMonitor.Value ??= NumberMonitorStateScriptNodeData.CreateDefaultValueExpression();
                        break;
                }
            }
        }

        private static ConditionConfig EnsureNecessaryCondition(ConditionConfig condition)
        {
            condition ??= new ConditionConfig();
            condition.ConditionType = ConditionType.Necessary;
            return condition;
        }
    }

    [Serializable]
    public sealed class StateScriptEdgeData
    {
        public string OutputNodeGuid;
        public string OutputPortName;
        public string InputNodeGuid;
        public string InputPortName;
    }

    [Serializable]
    [JsonConverter(typeof(StateScriptNodeDataConverter))]
    public abstract class StateScriptNodeData
    {
        public string Type;
        public string Guid;
        public Vector2 EditorPosition;
        public GameWorldExecutionTarget ExecutionTargets =
            GameWorldExecutionTarget.Standalone | GameWorldExecutionTarget.Server;
    }

    [Serializable]
    public abstract class EntryStateScriptNodeData : StateScriptNodeData
    {
    }

    [Serializable]
    public abstract class StateStateScriptNodeData : StateScriptNodeData
    {
        public int TickOrder;
    }

    [Serializable]
    public abstract class BoolStateScriptNodeData : StateScriptNodeData
    {
    }

    [Serializable]
    public abstract class ActionStateScriptNodeData : StateScriptNodeData
    {
    }

    [Serializable]
    [FactoryKey("Compare", 0, "Compare")]
    public sealed class CompareStateScriptNodeData : BoolStateScriptNodeData
    {
        public ConditionConfig Condition = new();

        public CompareStateScriptNodeData()
        {
            Type = "Compare";
        }
    }

    [Serializable]
    [FactoryKey("SetValue", 10, "Set Value")]
    public sealed class SetValueStateScriptNodeData : ActionStateScriptNodeData
    {
        public string SetterKey = string.Empty;
        public UnitSourceTarget SourceTarget = UnitSourceTarget.Self;
        public string Key = string.Empty;
        // Legacy one-input data; runtime migrates it into Values when binding old graphs.
        public ValueExpression Value = new();
        public List<ValueExpression> Values = new();

        public SetValueStateScriptNodeData()
        {
            Type = "SetValue";
        }

        public List<ValueExpression> GetOrCreateValues(int count)
        {
            Values ??= new List<ValueExpression>();
            if (Values.Count == 0 && Value != null)
                Values.Add(Value);

            while (Values.Count < count)
                Values.Add(new ValueExpression());

            return Values;
        }
    }

    [Serializable]
    public sealed class SkillRequestInputData
    {
        public ValueExpression Position = CreateDefaultPositionExpression();
        public ValueExpression TargetEntity = CreateDefaultTargetEntityExpression();

        public void EnsureValid()
        {
            Position ??= CreateDefaultPositionExpression();
            TargetEntity ??= CreateDefaultTargetEntityExpression();
        }

        public static SkillRequestInputData CreateDefault()
        {
            return new SkillRequestInputData();
        }

        private static ValueExpression CreateDefaultPositionExpression()
        {
            return new ValueExpression
            {
                Literal = UnitValue.FromFloat3(float3.zero),
            };
        }

        private static ValueExpression CreateDefaultTargetEntityExpression()
        {
            return new ValueExpression
            {
                Literal = UnitValue.FromEntity(Entity.Null),
            };
        }
    }

    [Serializable]
    [FactoryKey("RequestSkill", 11, "Request Skill")]
    public sealed class RequestSkillActionNodeData : ActionStateScriptNodeData
    {
        public ValueExpression SkillId = CreateDefaultSkillIdExpression();
        public SkillRequestInputData Input = SkillRequestInputData.CreateDefault();

        public RequestSkillActionNodeData()
        {
            Type = "RequestSkill";
        }

        public static ValueExpression CreateDefaultSkillIdExpression()
        {
            return new ValueExpression
            {
                Literal = UnitValue.FromInt(-1),
            };
        }
    }

    [Serializable]
    [FactoryKey("RequestSkillWithAddition", 13, "Request Skill With Addition")]
    public sealed class RequestSkillWithAdditionActionNodeData : ActionStateScriptNodeData
    {
        public ValueExpression SkillId = CreateDefaultSkillIdExpression();
        public SkillRequestInputData Input = SkillRequestInputData.CreateDefault();

        public RequestSkillWithAdditionActionNodeData()
        {
            Type = "RequestSkillWithAddition";
        }

        public static ValueExpression CreateDefaultSkillIdExpression()
        {
            return new ValueExpression
            {
                Kind = ValueExpressionKind.Getter,
                GetterKey = "player.skill.currentSkillId",
            };
        }
    }

    [Serializable]
    [FactoryKey("RequestInteraction", 14, "Request Interaction")]
    public sealed class RequestInteractionActionNodeData : ActionStateScriptNodeData
    {
        public ValueExpression Target = CreateDefaultTargetExpression();

        public RequestInteractionActionNodeData()
        {
            Type = "RequestInteraction";
        }

        public static ValueExpression CreateDefaultTargetExpression()
        {
            return new ValueExpression { Literal = UnitValue.FromEntity(Entity.Null) };
        }
    }

    [Serializable]
    [FactoryKey("CompleteInteraction", 19, "Complete Interaction")]
    public sealed class CompleteInteractionActionNodeData : ActionStateScriptNodeData
    {
        public InteractionResultCode ResultCode = InteractionResultCode.Success;
        public ValueExpression Result = CreateDefaultResultExpression();

        public CompleteInteractionActionNodeData()
        {
            Type = "CompleteInteraction";
        }

        public static ValueExpression CreateDefaultResultExpression()
        {
            return new ValueExpression { Literal = UnitValue.FromBool(true) };
        }
    }

    [Serializable]
    [FactoryKey("AcknowledgeInteraction", 25, "Acknowledge Interaction")]
    public sealed class AcknowledgeInteractionActionNodeData : ActionStateScriptNodeData
    {
        public AcknowledgeInteractionActionNodeData()
        {
            Type = "AcknowledgeInteraction";
        }
    }

    [Serializable]
    [FactoryKey("CollectInteraction", 26, "Collect Interaction")]
    public sealed class CollectInteractionActionNodeData : ActionStateScriptNodeData
    {
        public CollectInteractionActionNodeData()
        {
            Type = "CollectInteraction";
        }
    }

    [Serializable]
    [FactoryKey("StartNpcInteraction", 27, "Start NPC Interaction")]
    public sealed class StartNpcInteractionActionNodeData : ActionStateScriptNodeData
    {
        public StartNpcInteractionActionNodeData()
        {
            Type = "StartNpcInteraction";
        }
    }

    [Serializable]
    [FactoryKey("SpawnUnit", 15, "Spawn Unit")]
    public sealed class SpawnUnitActionNodeData : ActionStateScriptNodeData
    {
        // When set, entries are read from UnitVariableComponent using:
        // <key>.count and <key>.<index>.(unit|position|monster...).
        public string VariableListKey = string.Empty;
        public string UnitName = string.Empty;
        public string[] CandidateUnitNames = Array.Empty<string>();
        public int Count = 1;
        public float SpawnRadius = 1f;
        public float MinSpawnRadius;
        public Vector3 CenterOffset;
        public bool CopyFactionFromSpawner = true;
        // Keeps spawned variables local and exposes the spawner through UnitSourceTarget.Other.
        public bool ShareVariablesWithSpawner;
        public bool RestoreRuntimeState;

        public SpawnUnitActionNodeData()
        {
            Type = "SpawnUnit";
        }
    }

    [Serializable]
    public enum StateScriptUnitQuerySortMode : byte
    {
        None,
        DistanceAscending,
        DistanceDescending,
    }

    [Serializable]
    [FactoryKey("QueryUnits", 16, "Query Units")]
    public sealed class QueryUnitsActionNodeData : ActionStateScriptNodeData
    {
        public UnitQueryShapeType Shape = UnitQueryShapeType.WholeWorld;
        public ValueExpression Center = CreateDefaultCenterExpression();
        public ValueExpression Direction = CreateDefaultDirectionExpression();
        public ValueExpression Size = CreateDefaultSizeExpression();
        public ValueExpression Radius = CreateDefaultRadiusExpression();
        public ValueExpression Angle = CreateDefaultAngleExpression();
        public UnitFactionMask FactionMask = UnitFactionMask.All;
        public int UnitDataId = -1;
        public bool ExcludeSelf = true;
        public bool ExcludeDead = true;
        // Optional UnitVariable entity-list prefix: <key>.count and <key>.<index>.
        public string ExcludedEntitiesKey = string.Empty;
        public bool RememberResultsInExcludedEntities;
        public bool RequireAvailableInteraction;
        public int MaxCount;
        public StateScriptUnitQuerySortMode SortMode;
        public string ResultKey = string.Empty;

        public QueryUnitsActionNodeData()
        {
            Type = "QueryUnits";
        }

        public void EnsureValid()
        {
            Center ??= CreateDefaultCenterExpression();
            Direction ??= CreateDefaultDirectionExpression();
            Size ??= CreateDefaultSizeExpression();
            Radius ??= CreateDefaultRadiusExpression();
            Angle ??= CreateDefaultAngleExpression();
            MaxCount = math.max(0, MaxCount);
            ResultKey ??= string.Empty;
            ExcludedEntitiesKey ??= string.Empty;
        }

        public static ValueExpression CreateDefaultCenterExpression()
        {
            return new ValueExpression
            {
                Kind = ValueExpressionKind.Getter,
                GetterKey = "unit.transform.position",
            };
        }

        public static ValueExpression CreateDefaultDirectionExpression()
        {
            return new ValueExpression { Literal = UnitValue.FromFloat2(new float2(1f, 0f)) };
        }

        public static ValueExpression CreateDefaultSizeExpression()
        {
            return new ValueExpression { Literal = UnitValue.FromFloat2(new float2(1f)) };
        }

        public static ValueExpression CreateDefaultRadiusExpression()
        {
            return new ValueExpression { Literal = UnitValue.FromFloat(1f) };
        }

        public static ValueExpression CreateDefaultAngleExpression()
        {
            return new ValueExpression { Literal = UnitValue.FromFloat(90f) };
        }
    }

    [Serializable]
    public enum StateScriptEffectOriginSource : byte
    {
        None,
        Self,
        Other,
    }

    [Serializable]
    [FactoryKey("ExecuteEffect", 17, "Execute Effect")]
    public sealed class ExecuteEffectActionNodeData : ActionStateScriptNodeData
    {
        [SerializeReference]
        public EffectData[] Effects = Array.Empty<EffectData>();
        public StateScriptEffectOriginSource OriginSource = StateScriptEffectOriginSource.Self;
        public ValueExpression TargetEntity = CreateDefaultEntityExpression();
        public ValueExpression OtherEntity = CreateDefaultEntityExpression();
        public ValueExpression Position = QueryUnitsActionNodeData.CreateDefaultCenterExpression();
        public ValueExpression TriggerValue = CreateDefaultTriggerValueExpression();
        public ValueExpression SourceSkillId = CreateDefaultSourceSkillIdExpression();
        public int RepeatCount = 1;

        public ExecuteEffectActionNodeData()
        {
            Type = "ExecuteEffect";
        }

        public void EnsureValid()
        {
            Effects ??= Array.Empty<EffectData>();
            TargetEntity ??= CreateDefaultEntityExpression();
            OtherEntity ??= CreateDefaultEntityExpression();
            Position ??= QueryUnitsActionNodeData.CreateDefaultCenterExpression();
            TriggerValue ??= CreateDefaultTriggerValueExpression();
            SourceSkillId ??= CreateDefaultSourceSkillIdExpression();
            RepeatCount = math.max(1, RepeatCount);
        }

        public static ValueExpression CreateDefaultEntityExpression()
        {
            return new ValueExpression { Literal = UnitValue.FromEntity(Entity.Null) };
        }

        public static ValueExpression CreateDefaultTriggerValueExpression()
        {
            return new ValueExpression { Literal = UnitValue.FromFloat(0f) };
        }

        public static ValueExpression CreateDefaultSourceSkillIdExpression()
        {
            return new ValueExpression { Literal = UnitValue.FromInt(-1) };
        }
    }

    [Serializable]
    [FactoryKey("DestroySelf", 18, "Destroy Self")]
    public sealed class DestroySelfActionNodeData : ActionStateScriptNodeData
    {
        public DestroySelfActionNodeData()
        {
            Type = "DestroySelf";
        }
    }

    [Serializable]
    [FactoryKey("PublishGameEvent", 12, "Publish Game Event")]
    public sealed class PublishGameEventStateScriptNodeData : ActionStateScriptNodeData
    {
        public string EventName = string.Empty;
        public ValueExpression Reference = CreateDefaultReferenceExpression();

        public PublishGameEventStateScriptNodeData()
        {
            Type = "PublishGameEvent";
        }

        public static ValueExpression CreateDefaultReferenceExpression()
        {
            return new ValueExpression
            {
                Literal = UnitValue.None,
            };
        }
    }

    [Serializable]
    [FactoryKey("Timer", 20, "Timer")]
    public sealed class TimerStateScriptNodeData : StateStateScriptNodeData
    {
        public ValueExpression Duration = CreateDefaultDurationExpression();

        public TimerStateScriptNodeData()
        {
            Type = "Timer";
        }

        public static ValueExpression CreateDefaultDurationExpression()
        {
            return new ValueExpression
            {
                Literal = UnitValue.FromFloat(1f),
            };
        }
    }

    [Serializable]
    [FactoryKey("Keep", 21, "Keep")]
    public sealed class KeepStateScriptNodeData : StateStateScriptNodeData
    {
        public float DurationSeconds = 1f;

        public KeepStateScriptNodeData()
        {
            Type = "Keep";
        }
    }

    [Serializable]
    [FactoryKey("Monitor", 22, "Monitor")]
    public sealed class MonitorStateScriptNodeData : StateStateScriptNodeData
    {
        public ConditionConfig Condition = new();

        public MonitorStateScriptNodeData()
        {
            Type = "Monitor";
        }
    }

    [Serializable]
    [FactoryKey("NumberMonitor", 23, "Number Monitor")]
    public sealed class NumberMonitorStateScriptNodeData : StateStateScriptNodeData
    {
        public ValueExpression Value = CreateDefaultValueExpression();

        public NumberMonitorStateScriptNodeData()
        {
            Type = "NumberMonitor";
        }

        public static ValueExpression CreateDefaultValueExpression()
        {
            return new ValueExpression
            {
                Literal = UnitValue.FromFloat(0f),
            };
        }
    }

    [Serializable]
    [FactoryKey("Addition", 24, "Addition")]
    public sealed class AdditionStateScriptNodeData : StateStateScriptNodeData
    {
        public string EventName = string.Empty;

        public AdditionStateScriptNodeData()
        {
            Type = "Addition";
        }
    }

    [Serializable]
    [FactoryKey("PlayerInputEvent", 28, "Player Input Event")]
    public sealed class PlayerInputEventStateScriptNodeData : StateStateScriptNodeData
    {
        public PlayerInputOperationType EventType;
        // 仅主操作可额外保持原有的按住连续施法；当帧已有点击时不重复触发。
        public bool RepeatWhileHeld;

        public PlayerInputEventStateScriptNodeData()
        {
            Type = "PlayerInputEvent";
        }
    }

    [Serializable]
    [FactoryKey("Entry", -100, "Entry")]
    public sealed class StateScriptEntryNodeData : EntryStateScriptNodeData
    {
        public StateScriptEntryNodeData()
        {
            Type = "Entry";
        }
    }
}
