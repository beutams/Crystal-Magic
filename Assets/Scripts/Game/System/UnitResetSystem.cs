using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Server;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateAfter(typeof(UnitQueryBuildSystem))]
[UpdateBefore(typeof(PlayerEquipmentPropertySystem))]
[UpdateBefore(typeof(UnitBuffSystem))]
partial class UnitResetSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (FrameManagerUtility.TryGet(EntityManager, out ClientFrameManager _))
            return;

        foreach (RefRW<UnitMoveComponent> moveRef in SystemAPI.Query<RefRW<UnitMoveComponent>>())
        {
            UnitMoveComponent move = moveRef.ValueRW;
            if (math.lengthsq(move.Direction) <= 0.0001f)
                continue;

            move.Direction = float2.zero;
            move.NetworkDirty = 1;
            moveRef.ValueRW = move;
        }

    }
}
