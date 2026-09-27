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
            AddComponent<UnitInitializationPendingTag>(entity);
            AddBuffer<StateScriptGraphStateElement>(entity);
            AddBuffer<StateScriptNodeStateElement>(entity);
            AddBuffer<StateScriptSourceCommandElement>(entity);
            AddBuffer<StateScriptSourceCommandArgumentElement>(entity);
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
    public byte IsStoppedForDeath;
}

[UnitSourceProvider(typeof(UnitStateScriptComponent), typeof(UnitStateScriptAuthoring))]
public static class UnitSelfSource
{
    [UnitSourceGet(0, "unit.self.entity", UnitValueCategory.Entity)]
    public static bool TryGet(
        int operation,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        bool valid = operation == 0 && entity != Entity.Null;
        result = valid ? UnitSourceValue.FromEntity(entity) : default;
        return valid;
    }
}
