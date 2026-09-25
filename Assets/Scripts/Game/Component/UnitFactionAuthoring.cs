using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public enum UnitFactionType
{
    Player = 0,
    Friend = 1,
    Enemy = 2,
    Boss = 3,
    Interactable = 4,
}

public class UnitFactionAuthoring : MonoBehaviour
{
    class UnitFactionBaker : Baker<UnitFactionAuthoring>
    {
        public override void Bake(UnitFactionAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            UnitFactionType faction = UnitFactionType.Friend;
            UnitFactionModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitFactionModuleData>(authoring);
            if (data != null)
                faction = data.Faction;

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitFactionComponent
            {
                Value = faction,
            });
        }
    }
}

public struct UnitFactionComponent : IComponentData
{
    public UnitFactionType Value;
}

[UnitSourceProvider(typeof(UnitFactionComponent), typeof(UnitFactionAuthoring))]
public static class UnitFactionSource
{
    [UnitSourceGet(0, "unit.faction.value", UnitValueCategory.Number)]
    public static bool TryGet(
        int operation,
        in UnitFactionComponent value,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation == 0 ? UnitSourceValue.FromInt((int)value.Value) : UnitSourceValue.None;
        return result.Type != UnitValueType.None;
    }

    [UnitSourceGet(1, "unit.faction.isEnemyTo", UnitValueCategory.Bool, UnitValueCategory.Entity,
        ParameterNames = new[] { "Other Entity" })]
    public static bool TryGetRelation(
        int operation,
        Entity entity,
        in ComponentLookup<UnitFactionComponent> factions,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = UnitSourceValue.FromBool(false);
        if (operation != 1 || !factions.TryGetComponent(entity, out UnitFactionComponent faction))
            return false;

        if (!arguments.TryGetEntity(0, out Entity otherEntity) || otherEntity == Entity.Null ||
            !factions.TryGetComponent(otherEntity, out UnitFactionComponent otherFaction))
            return true;

        result = UnitSourceValue.FromBool(UnitFactionUtility.IsEnemy(faction.Value, otherFaction.Value));
        return true;
    }
}
