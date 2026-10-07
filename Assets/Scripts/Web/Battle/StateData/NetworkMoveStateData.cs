using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public sealed class NetworkMoveStateData : NetworkStateData
{
    public float baseMoveSpeed;
    public float baseMoveSpeedOffset;
    public float baseMaxAcceleration;
    public float stateMoveMultiplier;
    public float velocityX;
    public float velocityY;
    public float positionX;
    public float positionY;
    public float positionZ;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        // Capture the present prediction before replacing it with a historical state.
        context.ApplyPosition(entity, new float3(positionX, positionY, positionZ));
        context.SetOrAdd(entity, new UnitMoveComponent
        {
            BaseMoveSpeed = baseMoveSpeed,
            BaseMoveSpeedOffset = baseMoveSpeedOffset,
            BaseMaxAcceleration = baseMaxAcceleration,
            StateMoveMultiplier = stateMoveMultiplier,
            Velocity = new float2(velocityX, velocityY),
            PredictedPosition = new float3(positionX, positionY, positionZ),
            HasPredictedPosition = 1,
            LastObservedPosition = new float3(positionX, positionY, positionZ),
            CommandMoveSpeed = -1f,
            NetworkDirty = 0,
        });
    }
}
