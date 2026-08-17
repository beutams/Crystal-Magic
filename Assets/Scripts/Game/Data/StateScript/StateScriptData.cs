using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    [ReadOnlyData]
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
        public string EntryNodeGuid;
        public List<StateScriptNodeData> Nodes = new();
        public List<StateScriptEdgeData> Edges = new();
        public Vector2 ViewPosition;
        public float ViewScale = 1f;

        public void EnsureValid()
        {
            Guid ??= System.Guid.NewGuid().ToString("N");
            Name ??= string.Empty;
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
                        break;
                    case PublishGameEventStateScriptNodeData publishGameEvent:
                        publishGameEvent.Payload ??= PublishGameEventStateScriptNodeData.CreateDefaultPayloadExpression();
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
    }

    [Serializable]
    public abstract class EntryStateScriptNodeData : StateScriptNodeData
    {
    }

    [Serializable]
    public abstract class StateStateScriptNodeData : StateScriptNodeData
    {
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
        public string Key = string.Empty;
        public ValueExpression Value = new();

        public SetValueStateScriptNodeData()
        {
            Type = "SetValue";
        }
    }

    [Serializable]
    [FactoryKey("RequestSkill", 11, "Request Skill")]
    public sealed class RequestSkillActionNodeData : ActionStateScriptNodeData
    {
        public ValueExpression SkillId = CreateDefaultSkillIdExpression();

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
    [FactoryKey("PublishGameEvent", 12, "Publish Game Event")]
    public sealed class PublishGameEventStateScriptNodeData : ActionStateScriptNodeData
    {
        public string EventName = string.Empty;
        public ValueExpression Payload = CreateDefaultPayloadExpression();

        public PublishGameEventStateScriptNodeData()
        {
            Type = "PublishGameEvent";
        }

        public static ValueExpression CreateDefaultPayloadExpression()
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
    [FactoryKey("Entry", -100, "Entry")]
    public sealed class StateScriptEntryNodeData : EntryStateScriptNodeData
    {
        public StateScriptEntryNodeData()
        {
            Type = "Entry";
        }
    }
}
