using System;
using System.Collections.Generic;
using NUnit.Framework;

public sealed class UnitRuntimeBehaviorTests
{
    [Test]
    public void ChantDurationMultiplier_ConvertsChantSpeedBonus()
    {
        Assert.That(UnitAttackComponent.GetDurationMultiplier(0f), Is.EqualTo(1f));
        Assert.That(UnitAttackComponent.GetDurationMultiplier(100f), Is.EqualTo(0.5f));
        Assert.That(UnitAttackComponent.GetDurationMultiplier(-50f), Is.EqualTo(1.5f));
    }

    [Test]
    public void AttackSource_OnlyExposesBaseRealAndChantMultiplier()
    {
        UnitSourceSchema schema = Describe(new UnitAttackSource());

        Assert.That(HasGet(schema, "unit.attack.baseAttackPower"), Is.True);
        Assert.That(HasGet(schema, "unit.attack.realAttackPower"), Is.True);
        Assert.That(HasGet(schema, "unit.attack.chantDurationMultiplier"), Is.True);
        Assert.That(HasGet(schema, "unit.attack.actionDurationMultiplier"), Is.False);
        Assert.That(HasGet(schema, "unit.attack.attackFactor"), Is.False);
    }

    [Test]
    public void PerceptionSource_ExposesNearbyEntityListInsteadOfSingleTarget()
    {
        UnitSourceSchema schema = Describe(new UnitPerceptionSource());

        Assert.That(HasGet(schema, "unit.perception.searchRadius"), Is.True);
        Assert.That(HasGet(schema, "unit.perception.entityCount"), Is.True);
        Assert.That(HasGet(schema, "unit.perception.entityAt"), Is.True);
        Assert.That(HasGet(schema, "unit.perception.hasTarget"), Is.False);
        Assert.That(HasGet(schema, "unit.perception.targetEntity"), Is.False);
    }

    [Test]
    public void VitalityMoveAndFacingSources_ExposeOnlyTheApprovedAccessors()
    {
        UnitSourceSchema vitality = Describe(new UnitVitalitySource());
        Assert.That(HasGet(vitality, "unit.vitality.baseMaxHealth"), Is.True);
        Assert.That(HasGet(vitality, "unit.vitality.realMaxHealth"), Is.True);
        Assert.That(HasGet(vitality, "unit.vitality.currentHealth"), Is.True);
        Assert.That(HasGet(vitality, "unit.vitality.currentHealthPercentage"), Is.True);
        Assert.That(HasGet(vitality, "unit.vitality.healthFactor"), Is.False);

        UnitSourceSchema move = Describe(new UnitMoveSource());
        Assert.That(HasGet(move, "unit.move.baseMoveSpeed"), Is.True);
        Assert.That(HasGet(move, "unit.move.realMoveSpeed"), Is.True);
        Assert.That(HasGet(move, "unit.move.direction"), Is.True);
        Assert.That(HasGet(move, "unit.move.stateMoveMultiplier"), Is.True);
        Assert.That(HasGet(move, "unit.move.velocity"), Is.False);
        Assert.That(HasSet(move, "unit.move.setVelocity"), Is.True);

        UnitSourceSchema facing = Describe(new UnitFacingSource());
        Assert.That(HasGet(facing, "unit.facing.direction"), Is.True);
        Assert.That(HasGet(facing, "unit.facing.angleDegrees"), Is.False);
        Assert.That(HasSet(facing, "unit.facing.direction"), Is.False);
    }

    [Test]
    public void RemoveStacks_OnlyConsumesWhenTheRequestedCountIsAvailable()
    {
        UnitSourceSchema schema = Describe(new UnitBuffSource());
        Assert.That(schema.TryGet("unit.buffs.removeStacks", out UnitSourceSetSchemaEntry removeStacks), Is.True);
        Assert.That(removeStacks.Parameters, Has.Count.EqualTo(2));

        UnitBuffRuntimeComponent component = new()
        {
            Buffs = new List<UnitBuffRuntimeEntry>
            {
                new() { BuffId = 42, StackCount = 3 },
            },
        };

        Assert.That(InvokeRemoveStacks(component, 42, 2), Is.True);
        Assert.That(component.Buffs, Has.Count.EqualTo(1));
        Assert.That(component.Buffs[0].StackCount, Is.EqualTo(1));

        Assert.That(InvokeRemoveStacks(component, 42, 2), Is.False);
        Assert.That(component.Buffs, Has.Count.EqualTo(1));
        Assert.That(component.Buffs[0].StackCount, Is.EqualTo(1));

        Assert.That(InvokeRemoveStacks(component, 42, 1), Is.True);
        Assert.That(component.Buffs, Is.Empty);
    }

    private static UnitSourceSchema Describe(UnitComponentSource source)
    {
        UnitSourceSchemaBuilder builder = new();
        source.Describe(builder);
        return builder.Build();
    }

    private static bool HasGet(UnitSourceSchema schema, string key)
    {
        return schema.TryGet(key, out UnitSourceGetSchemaEntry _);
    }

    private static bool HasSet(UnitSourceSchema schema, string key)
    {
        return schema.TryGet(key, out UnitSourceSetSchemaEntry _);
    }

    private static bool InvokeRemoveStacks(UnitBuffRuntimeComponent component, int buffId, int stackCount)
    {
        var method = typeof(UnitBuffSource).GetMethod(
            "TryRemoveStacks",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "UnitBuffSource must expose the two-input remove-stacks operation.");

        return (bool)method.Invoke(null, new object[] { component, buffId, stackCount });
    }
}
