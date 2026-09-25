using Unity.Entities;

public enum InteractionPhase : byte
{
    Pending = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3,
}

public enum InteractionResultCode : byte
{
    None = 0,
    Success = 1,
    Failed = 2,
    InvalidTarget = 3,
    Busy = 4,
    Cancelled = 5,
}

[InternalBufferCapacity(4)]
public struct InteractionTransactionElement : IBufferElementData
{
    public uint RequestId;
    public Entity Actor;
    public Entity Target;
    public InteractionPhase Phase;
    public InteractionResultCode ResultCode;
    public UnitSourceValue Result;
}
