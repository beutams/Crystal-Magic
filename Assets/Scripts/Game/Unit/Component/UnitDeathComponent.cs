using System;
using Unity.Entities;

public struct UnitDeathComponent : IComponentData, IEnableableComponent
{
}

[UnitSourceAuthoring(typeof(UnitDeathAuthoring))]
public sealed class UnitDeathSource : UnitComponentSource
{
    private static readonly ComparatorParameterDefinition[] s_noParameters = Array.Empty<ComparatorParameterDefinition>();

    public override Type ComponentType => typeof(UnitDeathComponent);

    public override void Describe(UnitSourceSchemaBuilder schema)
    {
        schema.AddGet("unit.death.isActive", ComponentType, UnitValueCategory.Bool, s_noParameters);
    }

    public override void Bind(in UnitSourceBindingContext context, UnitSourceAccessTable table)
    {
        EntityManager entityManager = context.EntityManager;
        Entity entity = context.Entity;
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitDeathComponent>(entity))
            return;

        table.AddGet(new UnitSourceGet(
            "unit.death.isActive",
            UnitValueCategory.Bool,
            s_noParameters,
            _ => UnitValue.FromBool(entityManager.Exists(entity) && entityManager.IsComponentEnabled<UnitDeathComponent>(entity))));
    }
}
