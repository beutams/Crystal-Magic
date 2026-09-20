using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public enum StateScriptInitializationError : byte
{
    None,
    MissingUnitDataId,
    DefinitionNotFound,
    InvalidDefinition,
}

public enum StateScriptStateStatus : byte
{
    Stop,
    Pending,
    Running,
}

public enum StateScriptNodeRuntimeType : byte
{
    Entry,
    Compare,
    SetValue,
    RequestSkill,
    PublishGameEvent,
    RequestSkillWithAddition,
    RequestInteraction,
    SpawnUnit,
    Timer,
    Keep,
    Monitor,
    NumberMonitor,
    Addition,
}

public enum StateScriptManagedCommandType : byte
{
    RequestSkill,
    RequestSkillWithAddition,
    PublishGameEvent,
    RequestInteraction,
    SpawnUnit,
    StartAddition,
    StopAddition,
}

public enum StateScriptExternalResultStatus : byte
{
    Completed,
}

public static class StateScriptPortId
{
    public const byte In = 0;
    public const byte Start = 0;
    public const byte Abort = 1;
    public const byte Keep = 2;

    public const byte Out = 0;
    public const byte True = 0;
    public const byte False = 1;
    public const byte OnStart = 0;
    public const byte OnTick = 1;
    public const byte OnComplete = 2;
    public const byte OnAbort = 3;
    public const byte OnStop = 4;
    public const byte OnTimeStart = 5;
    public const byte OnTimeTick = 6;
    public const byte OnTimeComplete = 7;
    public const byte OnTimeStop = 8;
    public const byte MonitorTrue = 5;
    public const byte MonitorFalse = 6;
    public const byte OnChangeTrue = 7;
    public const byte OnChangeFalse = 8;
    public const byte OnValueChange = 5;
}

public struct StateScriptNodeDefinition
{
    public StateScriptNodeRuntimeType Type;
    public int ExpressionStart;
    public byte ExpressionCount;
    public int OutputRouteStart;
    public ushort OutputRouteCount;
    public int StringStart;
    public ushort StringCount;
    public int TickOrder;
    public UnitSourceId SetSourceId;
    public UnitSourceTarget SetSourceTarget;
    public FixedString128Bytes Key;
    public FixedString128Bytes Text;
    public UnitInteractionData InteractionData;
    public float4 FloatParameters0;
    public float4 FloatParameters1;
    public int4 IntParameters;
    public FixedString128Bytes Guid;
}

public struct StateScriptOutputRoute
{
    public byte OutputPortId;
    public int TargetStart;
    public ushort TargetCount;
}

public struct StateScriptPulseTarget
{
    public int NodeIndex;
    public byte InputPortId;
}

public struct StateScriptGraphDefinitionBlob
{
    public int EntryNodeIndex;
    public int ExecutionConditionExpressionIndex;
    public BlobArray<StateScriptNodeDefinition> Nodes;
    public BlobArray<StateScriptOutputRoute> OutputRoutes;
    public BlobArray<StateScriptPulseTarget> PulseTargets;
    public BlobArray<int> StateNodeIndices;
    public BlobArray<BehaviorExpressionBlob> Expressions;
    public BlobArray<FixedString128Bytes> Strings;
    public FixedString128Bytes Guid;
    public FixedString128Bytes Name;
}

public struct StateScriptUnitDefinitionBlob
{
    public int UnitDataId;
    public BlobArray<StateScriptGraphDefinitionBlob> Graphs;
}

public struct StateScriptRuntimeRegistryBlob
{
    public BlobArray<StateScriptUnitDefinitionBlob> Units;
}

public struct StateScriptRuntimeRegistryComponent : IComponentData
{
    public BlobAssetReference<StateScriptRuntimeRegistryBlob> Value;
}

[InternalBufferCapacity(0)]
public struct StateScriptGraphStateElement : IBufferElementData
{
    public int NodeStateStart;
    public byte IsActive;
}

[InternalBufferCapacity(0)]
public struct StateScriptNodeStateElement : IBufferElementData
{
    public StateScriptStateStatus Status;
    public float Time;
    public float Auxiliary;
    public uint PendingTick;
    public uint LastKeepTick;
    public uint TimingStartTick;
    public uint LastPulseTick;
    public byte Flags;
}

[InternalBufferCapacity(0)]
public struct StateScriptSourceCommandElement : IBufferElementData
{
    public UnitSourceId SourceId;
    public Entity TargetEntity;
    public int ArgumentStart;
    public ushort ArgumentCount;
    public FixedString128Bytes Key;
    public byte HasKey;
}

[InternalBufferCapacity(0)]
public struct StateScriptSourceCommandArgumentElement : IBufferElementData
{
    public UnitSourceValue Value;
}

[InternalBufferCapacity(0)]
public struct StateScriptManagedCommandElement : IBufferElementData
{
    public StateScriptManagedCommandType Type;
    public int GraphIndex;
    public int NodeIndex;
    public int IntValue;
    public float3 Position;
    public Entity TargetEntity;
    public UnitSourceValue Value;
}

[InternalBufferCapacity(0)]
public struct StateScriptExternalResultElement : IBufferElementData
{
    public int GraphIndex;
    public int NodeIndex;
    public StateScriptExternalResultStatus Status;
}

internal struct StateScriptPulse
{
    public int NodeIndex;
    public byte InputPortId;
}

internal readonly struct StateScriptActionKey : System.IEquatable<StateScriptActionKey>
{
    public StateScriptActionKey(Entity entity, int graphIndex, int nodeIndex)
    {
        Entity = entity;
        GraphIndex = graphIndex;
        NodeIndex = nodeIndex;
    }

    public Entity Entity { get; }
    public int GraphIndex { get; }
    public int NodeIndex { get; }

    public bool Equals(StateScriptActionKey other)
    {
        return Entity.Equals(other.Entity) && GraphIndex == other.GraphIndex && NodeIndex == other.NodeIndex;
    }

    public override bool Equals(object obj)
    {
        return obj is StateScriptActionKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Entity.GetHashCode();
            hash = hash * 397 ^ GraphIndex;
            return hash * 397 ^ NodeIndex;
        }
    }
}

internal static class StateScriptUnmanagedContract
{
    private static void Validate()
    {
        RequireUnmanaged<StateScriptNodeDefinition>();
        RequireUnmanaged<StateScriptGraphStateElement>();
        RequireUnmanaged<StateScriptNodeStateElement>();
        RequireUnmanaged<StateScriptSourceCommandElement>();
        RequireUnmanaged<StateScriptSourceCommandArgumentElement>();
        RequireUnmanaged<StateScriptManagedCommandElement>();
        RequireUnmanaged<StateScriptExternalResultElement>();
        RequireUnmanaged<UnitStateScriptComponent>();
    }

    private static void RequireUnmanaged<T>() where T : unmanaged
    {
    }
}
