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
        public int UnitDataId = -1;
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
        public List<ConditionConfig> Conditions = new();

        public CompareStateScriptNodeData()
        {
            Type = "Compare";
        }
    }

    [Serializable]
    [FactoryKey("SetValue", 10, "Set Value")]
    public sealed class SetValueStateScriptNodeData : ActionStateScriptNodeData
    {
        public string SetterKey = "unit.variables.set";
        public List<UnitValue> Arguments = new()
        {
            UnitValue.FromString(string.Empty),
            UnitValue.FromFloat(0f),
        };

        public SetValueStateScriptNodeData()
        {
            Type = "SetValue";
        }
    }

    public enum SkillReleaseTargetMode : byte
    {
        None = 0,
        Self = 1,
        Variables = 2,
        PerceptionTarget = 3,
    }

    [Serializable]
    [FactoryKey("RequestSkill", 11, "Request Skill")]
    public sealed class RequestSkillActionNodeData : ActionStateScriptNodeData
    {
        public int SkillId = -1;
        public int SkillAdditionId = -1;
        public SkillReleaseTargetMode TargetMode = SkillReleaseTargetMode.Variables;
        public string TargetPositionVariableKey = "skill.targetPosition";
        public string TargetEntityVariableKey = "skill.targetEntity";

        public RequestSkillActionNodeData()
        {
            Type = "RequestSkill";
        }
    }

    [Serializable]
    [FactoryKey("Timer", 20, "Timer")]
    public sealed class TimerStateScriptNodeData : StateStateScriptNodeData
    {
        public float DurationSeconds = 1f;

        public TimerStateScriptNodeData()
        {
            Type = "Timer";
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
        public List<ConditionConfig> Conditions = new();

        public MonitorStateScriptNodeData()
        {
            Type = "Monitor";
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
