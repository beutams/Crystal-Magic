using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[Serializable]
public sealed class NetworkMoveStateData : NetworkStateData
{
    public float baseMoveSpeed;
    public float baseMoveSpeedOffset;
    public float baseMaxAcceleration;
    public float directionX;
    public float directionY;
    public float stateMoveMultiplier;
    public float velocityX;
    public float velocityY;
    public float frameVelocityX;
    public float frameVelocityY;
    public byte hasFrameVelocity;
    public float commandMoveSpeed;
    public float positionX;
    public float positionY;
    public float positionZ;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        context.SetOrAdd(entity, new UnitMoveComponent
        {
            BaseMoveSpeed = baseMoveSpeed,
            BaseMoveSpeedOffset = baseMoveSpeedOffset,
            BaseMaxAcceleration = baseMaxAcceleration,
            Direction = new float2(directionX, directionY),
            StateMoveMultiplier = stateMoveMultiplier,
            Velocity = new float2(velocityX, velocityY),
            FrameVelocity = new float2(frameVelocityX, frameVelocityY),
            HasFrameVelocity = hasFrameVelocity,
            CommandMoveSpeed = commandMoveSpeed,
            NetworkDirty = 0,
        });

        if (context.EntityManager.HasComponent<LocalTransform>(entity))
        {
            LocalTransform transform = context.EntityManager.GetComponentData<LocalTransform>(entity);
            transform.Position = new float3(positionX, positionY, positionZ);
            context.EntityManager.SetComponentData(entity, transform);
        }
    }
}
