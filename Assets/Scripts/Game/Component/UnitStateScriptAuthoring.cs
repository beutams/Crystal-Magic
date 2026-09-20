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
            AddComponent(entity, new UnitStateScriptComponent
            {
                UnitDataId = unitData?.Id ?? -1,
                DefinitionIndex = -1,
            });
            AddBuffer<StateScriptGraphStateElement>(entity);
            AddBuffer<StateScriptNodeStateElement>(entity);
            AddBuffer<StateScriptSourceCommandElement>(entity);
            AddBuffer<StateScriptSourceCommandArgumentElement>(entity);
            AddBuffer<StateScriptManagedCommandElement>(entity);
            AddBuffer<StateScriptExternalResultElement>(entity);
        }
    }
}

public struct UnitStateScriptComponent : IComponentData
{
    public int UnitDataId;
    public int DefinitionIndex;
    public uint TickVersion;
    public StateScriptInitializationError InitializationError;
    public byte IsInitialized;
    public byte IsStoppedForDeath;
}
