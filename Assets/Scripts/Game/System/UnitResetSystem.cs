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
            bool hasDirection = math.lengthsq(move.Direction) > 0.0001f;
            bool hasCommandSpeed = move.CommandMoveSpeed >= 0f;
            if (!hasDirection && !hasCommandSpeed)
                continue;

            if (hasDirection)
                move.Direction = float2.zero;
            if (hasCommandSpeed)
                move.CommandMoveSpeed = -1f;
            move.NetworkDirty = 1;
            moveRef.ValueRW = move;
        }

    }
}
