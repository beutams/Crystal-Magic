using System;
using Unity.Entities;
using Unity.Mathematics;

public struct WorldInputComponent : IComponentData
{
    public float2 Move;
    public float3 PointerWorldPosition;
    public bool IsPrimaryHeld;
    public bool IsInteractHeld;
    public bool IsInventoryHeld;
    public bool IsPropertyHeld;
    public bool IsEscapeHeld;
    public bool IsSkillHeld;
    public int SkillChainIndex;
    public bool IsNextSkillChainHeld;
    public bool IsUsePropHeld;
    public int PropIndex;
}

public sealed class WorldInputSource : UnitComponentSource
{
    private static readonly ComparatorParameterDefinition[] s_noParameters = Array.Empty<ComparatorParameterDefinition>();

    public override Type ComponentType => typeof(WorldInputComponent);
    public override bool IsGlobal => true;

    public override void Describe(UnitSourceSchemaBuilder schema)
    {
        schema.AddGet("world.input.move", ComponentType, UnitValueCategory.Float2, s_noParameters);
        schema.AddGet("world.input.pointerWorldPosition", ComponentType, UnitValueCategory.Float3, s_noParameters);
        schema.AddGet("world.input.primaryHeld", ComponentType, UnitValueCategory.Bool, s_noParameters);
        schema.AddGet("world.input.interactHeld", ComponentType, UnitValueCategory.Bool, s_noParameters);
        schema.AddGet("world.input.inventoryHeld", ComponentType, UnitValueCategory.Bool, s_noParameters);
        schema.AddGet("world.input.propertyHeld", ComponentType, UnitValueCategory.Bool, s_noParameters);
        schema.AddGet("world.input.escapeHeld", ComponentType, UnitValueCategory.Bool, s_noParameters);
        schema.AddGet("world.input.skillHeld", ComponentType, UnitValueCategory.Bool, s_noParameters);
        schema.AddGet("world.input.skillChainIndex", ComponentType, UnitValueCategory.Number, s_noParameters);
        schema.AddGet("world.input.nextSkillChainHeld", ComponentType, UnitValueCategory.Bool, s_noParameters);
        schema.AddGet("world.input.usePropHeld", ComponentType, UnitValueCategory.Bool, s_noParameters);
        schema.AddGet("world.input.propIndex", ComponentType, UnitValueCategory.Number, s_noParameters);
    }

    public override void Bind(in UnitSourceBindingContext context, UnitSourceAccessTable table)
    {
        if (!WorldStateUtility.TryGetEntity(context.EntityManager, out Entity worldEntity))
            throw new InvalidOperationException("World state entity must exist before unit sources are initialized.");

        EntityManager entityManager = context.EntityManager;
        AddGet(table, entityManager, worldEntity, "world.input.move", UnitValueCategory.Float2,
            value => UnitValue.FromFloat2(value.Move));
        AddGet(table, entityManager, worldEntity, "world.input.pointerWorldPosition", UnitValueCategory.Float3,
            value => UnitValue.FromFloat3(value.PointerWorldPosition));
        AddGet(table, entityManager, worldEntity, "world.input.primaryHeld", UnitValueCategory.Bool,
            value => UnitValue.FromBool(value.IsPrimaryHeld));
        AddGet(table, entityManager, worldEntity, "world.input.interactHeld", UnitValueCategory.Bool,
            value => UnitValue.FromBool(value.IsInteractHeld));
        AddGet(table, entityManager, worldEntity, "world.input.inventoryHeld", UnitValueCategory.Bool,
            value => UnitValue.FromBool(value.IsInventoryHeld));
        AddGet(table, entityManager, worldEntity, "world.input.propertyHeld", UnitValueCategory.Bool,
            value => UnitValue.FromBool(value.IsPropertyHeld));
        AddGet(table, entityManager, worldEntity, "world.input.escapeHeld", UnitValueCategory.Bool,
            value => UnitValue.FromBool(value.IsEscapeHeld));
        AddGet(table, entityManager, worldEntity, "world.input.skillHeld", UnitValueCategory.Bool,
            value => UnitValue.FromBool(value.IsSkillHeld));
        AddGet(table, entityManager, worldEntity, "world.input.skillChainIndex", UnitValueCategory.Number,
            value => UnitValue.FromInt(value.SkillChainIndex));
        AddGet(table, entityManager, worldEntity, "world.input.nextSkillChainHeld", UnitValueCategory.Bool,
            value => UnitValue.FromBool(value.IsNextSkillChainHeld));
        AddGet(table, entityManager, worldEntity, "world.input.usePropHeld", UnitValueCategory.Bool,
            value => UnitValue.FromBool(value.IsUsePropHeld));
        AddGet(table, entityManager, worldEntity, "world.input.propIndex", UnitValueCategory.Number,
            value => UnitValue.FromInt(value.PropIndex));
    }

    private static void AddGet(
        UnitSourceAccessTable table,
        EntityManager entityManager,
        Entity worldEntity,
        string key,
        UnitValueCategory category,
        Func<WorldInputComponent, UnitValue> getter)
    {
        table.AddGet(new UnitSourceGet(
            key,
            category,
            s_noParameters,
            _ =>
            {
                if (!entityManager.Exists(worldEntity) || !entityManager.HasComponent<WorldInputComponent>(worldEntity))
                    return UnitValue.None;

                return getter(entityManager.GetComponentData<WorldInputComponent>(worldEntity));
            }));
    }
}
