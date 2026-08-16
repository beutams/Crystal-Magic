using CrystalMagic.Game.Data;
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
        foreach (RefRW<UnitMoveComponent> moveRef in SystemAPI.Query<RefRW<UnitMoveComponent>>())
        {
            UnitMoveComponent move = moveRef.ValueRW;
            move.Direction = float2.zero;
            moveRef.ValueRW = move;
        }

        foreach ((RefRW<UnitElementComponent> _, Entity entity) in
                 SystemAPI.Query<RefRW<UnitElementComponent>>().WithEntityAccess())
        {
            UnitModifierUtility.ResetFrameProperties(EntityManager, entity);
        }

        foreach (UnitSkillModifierRuntimeComponent runtimeComponent in
                 SystemAPI.Query<UnitSkillModifierRuntimeComponent>())
        {
            runtimeComponent.Modifiers = new SkillModifierSet();
        }
    }
}
