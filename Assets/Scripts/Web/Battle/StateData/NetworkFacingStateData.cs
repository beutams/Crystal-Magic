using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public sealed class NetworkFacingStateData : NetworkStateData
{
    public float directionX;
    public float directionY;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (context.TryGetEntity(unitId, out Entity entity))
            context.SetOrAdd(entity, new UnitFacingComponent
            {
                Direction = new float2(directionX, directionY),
                NetworkDirty = 0,
            });
    }
}
