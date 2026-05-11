using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public enum UnitFactionType
{
    Protagonist = 0,
    Friendly = 1,
    Enemy = 2,
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

            UnitFactionType faction = UnitFactionType.Friendly;
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

public static class UnitFactionUtility
{
    public static bool IsEnemy(UnitFactionType self, UnitFactionType other)
    {
        if (self == UnitFactionType.Enemy)
            return other != UnitFactionType.Enemy;

        return other == UnitFactionType.Enemy;
    }
}
