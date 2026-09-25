using CrystalMagic.Game.Data;
using Unity.Burst;
using Unity.Entities;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateAfter(typeof(UnitBuffSystem))]
public partial struct UnitModifierSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuffEffectRegistryComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        BlobAssetReference<BuffEffectRegistryBlob> registry =
            SystemAPI.GetSingleton<BuffEffectRegistryComponent>().Value;
        if (!registry.IsCreated)
            return;

        state.Dependency = new UnitModifierJob
        {
            Registry = registry,
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct UnitModifierJob : IJobEntity
{
    public BlobAssetReference<BuffEffectRegistryBlob> Registry;

    private void Execute(
        ref UnitBuffComponent buffState,
        ref UnitModifierComponent modifiers,
        in DynamicBuffer<UnitBuffElement> buffs)
    {
        if (buffState.ModifierDirty == 0)
            return;

        PropertyModifierSet propertyModifiers = default;
        SkillModifierSet skillModifiers = default;
        for (int buffIndex = 0; buffIndex < buffs.Length; buffIndex++)
        {
            UnitBuffElement buff = buffs[buffIndex];
            if (buff.StackCount <= 0 || !TryResolveDefinitionIndex(in buff, out int definitionIndex))
                continue;

            ref BuffDefinitionBlob definition = ref Registry.Value.Buffs[definitionIndex];
            for (int effectIndex = 0; effectIndex < definition.Effects.Length; effectIndex++)
            {
                BuffEffectReference effectReference = definition.Effects[effectIndex];
                if (effectReference.Type == BuffEffectType.PropertyModifier &&
                    effectReference.Id >= 0 &&
                    effectReference.Id < Registry.Value.PropertyModifiers.Length)
                {
                    PropertyModifierRuntimeEntry entry = Registry.Value.PropertyModifiers[effectReference.Id];
                    propertyModifiers.Add(in entry.Value, buff.StackCount, entry.MinimumFactor);
                }
                else if (effectReference.Type == BuffEffectType.SkillModifier &&
                         effectReference.Id >= 0 &&
                         effectReference.Id < Registry.Value.SkillModifiers.Length)
                {
                    SkillModifierRuntimeEntry entry = Registry.Value.SkillModifiers[effectReference.Id];
                    skillModifiers.Add(in entry.Value, buff.StackCount, entry.MinimumFactor);
                }
            }
        }

        modifiers = UnitModifierComponent.CreateIdentity();
        modifiers.MoveSpeed = propertyModifiers.GetValue(PropertyModifierChannel.MoveSpeed);
        modifiers.MaxHealth = propertyModifiers.GetValue(PropertyModifierChannel.MaxHealth);
        modifiers.Defense = propertyModifiers.GetValue(PropertyModifierChannel.Defense);
        modifiers.AttackPower = propertyModifiers.GetValue(PropertyModifierChannel.AttackPower);
        modifiers.SkillRange = propertyModifiers.GetValue(PropertyModifierChannel.SkillRange);
        modifiers.MaxMp = propertyModifiers.GetValue(PropertyModifierChannel.MaxMp);
        modifiers.HealthRegen = propertyModifiers.GetValue(PropertyModifierChannel.HealthRegen);
        modifiers.MpRegen = propertyModifiers.GetValue(PropertyModifierChannel.MpRegen);
        modifiers.ChantSpeed = propertyModifiers.GetValue(PropertyModifierChannel.ChantSpeed);
        modifiers.WaterPower = propertyModifiers.GetValue(PropertyModifierChannel.WaterPower);
        modifiers.FirePower = propertyModifiers.GetValue(PropertyModifierChannel.FirePower);
        modifiers.LightningPower = propertyModifiers.GetValue(PropertyModifierChannel.LightningPower);
        modifiers.WindPower = propertyModifiers.GetValue(PropertyModifierChannel.WindPower);
        modifiers.DamageTakenMultiplier = propertyModifiers.GetValue(PropertyModifierChannel.DamageTakenMultiplier);
        modifiers.SkillModifiers = skillModifiers;
        buffState.ModifierDirty = 0;
    }

    private readonly bool TryResolveDefinitionIndex(in UnitBuffElement buff, out int definitionIndex)
    {
        definitionIndex = buff.DefinitionIndex;
        if (definitionIndex >= 0 &&
            definitionIndex < Registry.Value.Buffs.Length &&
            Registry.Value.Buffs[definitionIndex].BuffId == buff.BuffId)
        {
            return true;
        }

        for (int i = 0; i < Registry.Value.Buffs.Length; i++)
        {
            if (Registry.Value.Buffs[i].BuffId != buff.BuffId)
                continue;

            definitionIndex = i;
            return true;
        }

        definitionIndex = -1;
        return false;
    }
}
