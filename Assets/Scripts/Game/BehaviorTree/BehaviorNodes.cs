using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public enum BehaviorNodeStatus : byte
{
    Success,
    Failure,
    Running,
}

public enum BehaviorNodeRuntimeType : byte
{
    Root,
    Selector,
    Sequence,
    Parallel,
    Inverter,
    Succeeder,
    Failer,
    Repeater,
    UntilSuccess,
    UntilFailure,
    Cooldown,
    Timeout,
    Check,
    HitCheck,
    Set,
    Wait,
    MoveTo,
}

public enum BehaviorTreeInitializationError : byte
{
    None,
    MissingUnitDataId,
    TreeNotFound,
    InvalidTree,
}

public struct BehaviorNodeDefinition
{
    public BehaviorNodeRuntimeType Type;
    public int ChildStart;
    public ushort ChildCount;
    public int ExpressionStart;
    public byte ExpressionCount;
    public UnitSourceId SetSourceId;
    public UnitSourceTarget SetSourceTarget;
    public FixedString128Bytes Key;
    public float4 FloatParameters0;
    public float4 FloatParameters1;
    public int4 IntParameters;
    public FixedString128Bytes Guid;
}

public struct BehaviorExpressionBlob
{
    public BlobArray<ExpressionInstruction> Instructions;
    public BlobArray<UnitSourceValue> Literals;
    public UnitValueCategory Category;
    public byte IsCondition;
}

public struct BehaviorTreeDefinitionBlob
{
    public int UnitDataId;
    public int RootNodeIndex;
    public BlobArray<BehaviorNodeDefinition> Nodes;
    public BlobArray<int> Children;
    public BlobArray<BehaviorExpressionBlob> Expressions;
}

public struct BehaviorTreeRuntimeRegistryBlob
{
    public BlobArray<BehaviorTreeDefinitionBlob> Trees;
}

public struct BehaviorTreeRuntimeRegistryComponent : IComponentData
{
    public BlobAssetReference<BehaviorTreeRuntimeRegistryBlob> Value;
}

[InternalBufferCapacity(0)]
public struct BehaviorNodeStateElement : IBufferElementData
{
    public int RunningChildIndex;
    public int Counter;
    public float Time;
    public float Auxiliary;
    public BehaviorNodeStatus LastStatus;
    public uint LastTickVersion;
    public byte Flags;

    public static BehaviorNodeStateElement CreateDefault()
    {
        return new BehaviorNodeStateElement
        {
            RunningChildIndex = -1,
            Auxiliary = 1f,
        };
    }
}

[InternalBufferCapacity(0)]
public struct BehaviorTreeCommandElement : IBufferElementData
{
    public UnitSourceId SourceId;
    public Entity TargetEntity;
    public int ArgumentStart;
    public ushort ArgumentCount;
    public FixedString128Bytes Key;
    public byte HasKey;
}

[InternalBufferCapacity(0)]
public struct BehaviorTreeMoveCommandElement : IBufferElementData
{
    public float3 Destination;
    public float StopDistance;
    public float Speed;
}

[InternalBufferCapacity(0)]
public struct BehaviorTreeHitDebugElement : IBufferElementData
{
    public float3 QueryOrigin;
    public float Length;
    public float Width;
    public float3 HitOrigin;
    public float3 HitPosition;
    public byte HasHit;
}

[InternalBufferCapacity(0)]
public struct BehaviorTreeCommandArgumentElement : IBufferElementData
{
    public UnitSourceValue Value;
}

internal static class BehaviorTreeUnmanagedContract
{
    private static void Validate()
    {
        RequireUnmanaged<BehaviorNodeDefinition>();
        RequireUnmanaged<BehaviorTreeInitializationError>();
        RequireUnmanaged<BehaviorNodeStateElement>();
        RequireUnmanaged<BehaviorTreeCommandElement>();
        RequireUnmanaged<BehaviorTreeCommandArgumentElement>();
        RequireUnmanaged<BehaviorTreeMoveCommandElement>();
        RequireUnmanaged<BehaviorTreeHitDebugElement>();
        RequireUnmanaged<UnitBehaviorTreeComponent>();
    }

    private static void RequireUnmanaged<T>() where T : unmanaged
    {
    }
}
