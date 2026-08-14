using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public sealed class UnitStateScriptAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitStateScriptAuthoring>
    {
        public override void Bake(UnitStateScriptAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            UnitData unitData = UnitAuthoringUtility.ResolveUnitData(authoring);
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new UnitStateScriptComponent
            {
                UnitDataId = unitData?.Id ?? -1,
            });
        }
    }
}

public sealed class UnitStateScriptComponent : IComponentData
{
    public int UnitDataId;
    public bool IsInitialized;
    public bool IsStoppedForDeath;
    public string InitializationError = string.Empty;
    public System.Collections.Generic.List<StateScriptRuntime> Runtimes = new();
}
