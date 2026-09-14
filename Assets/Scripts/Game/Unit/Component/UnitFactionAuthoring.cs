using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public enum UnitFactionType
{
    Player = 0,
    Friend = 1,
    Enemy = 2,
    Boss = 3,
    Npc = 4,
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

[UnitSourceAuthoring(typeof(UnitFactionAuthoring))]
public sealed class UnitFactionSource : UnitComponentSource<UnitFactionComponent>
{
    private static readonly ComparatorParameterDefinition[] s_otherEntityParameters =
    {
        new("Other Entity", UnitValueCategory.Entity),
    };

    protected override void Define(UnitSourceDefinitionBuilder<UnitFactionComponent> builder)
    {
        builder.AddGet("unit.faction.value", UnitValueCategory.Number,
            (in UnitFactionComponent value) => UnitValue.FromInt((int)value.Value));

        builder.AddContextGet("unit.faction.isEnemyTo", UnitValueCategory.Bool, s_otherEntityParameters,
            static (in UnitSourceBindingContext context, in UnitFactionComponent faction, UnitValue[] parameters) =>
            {
                if (parameters == null || parameters.Length != 1 ||
                    parameters[0].Type != UnitValueType.Entity)
                {
                    return UnitValue.FromBool(false);
                }

                Entity otherEntity = parameters[0].Entity;
                EntityManager entityManager = context.EntityManager;
                if (otherEntity == Entity.Null ||
                    !entityManager.Exists(otherEntity) ||
                    !entityManager.HasComponent<UnitFactionComponent>(otherEntity))
                {
                    return UnitValue.FromBool(false);
                }

                UnitFactionComponent otherFaction = entityManager.GetComponentData<UnitFactionComponent>(otherEntity);
                return UnitValue.FromBool(UnitFactionUtility.IsEnemy(faction.Value, otherFaction.Value));
            });
    }
}
