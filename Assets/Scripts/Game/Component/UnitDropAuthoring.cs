using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public sealed class UnitDropAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitDropAuthoring>
    {
        public override void Bake(UnitDropAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            UnitDropModuleData dropData = UnitAuthoringUtility.ResolveModuleData<UnitDropModuleData>(authoring);
            if (dropData == null || dropData.DropDataId < 0)
                return;

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitDropComponent
            {
                DropDataId = dropData.DropDataId,
            });
        }
    }
}

public struct UnitDropComponent : IComponentData
{
    public int DropDataId;
}
