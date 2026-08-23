using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateAfter(typeof(UnitBuffSystem))]
partial class UnitRecoverySystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<UnitVitalityComponent> vitalityRef, Entity entity) in
                 SystemAPI.Query<RefRW<UnitVitalityComponent>>().WithEntityAccess())
        {
            UnitVitalityComponent vitality = vitalityRef.ValueRO;
            float maxHealth = math.max(0f, UnitModifierResolver.GetMaxHealth(EntityManager, entity));
            float currentHealth = math.clamp(vitality.CurrentHealth, 0f, maxHealth);
            if (currentHealth <= 0f)
            {
                vitality.CurrentHealth = 0f;
                vitalityRef.ValueRW = vitality;
                continue;
            }

            float healthDelta = UnitModifierResolver.GetHealthRegen(EntityManager, entity) * deltaTime;
            vitality.CurrentHealth = math.clamp(currentHealth + healthDelta, 0f, maxHealth);
            vitalityRef.ValueRW = vitality;
        }

        foreach ((RefRW<UnitManaComponent> manaRef, Entity entity) in
                 SystemAPI.Query<RefRW<UnitManaComponent>>().WithEntityAccess())
        {
            UnitManaComponent mana = manaRef.ValueRO;
            float maxMp = math.max(0f, UnitModifierResolver.GetMaxMp(EntityManager, entity));
            float currentMana = math.clamp(mana.CurrentMana, 0f, maxMp);
            float manaDelta = UnitModifierResolver.GetMpRegen(EntityManager, entity) * deltaTime;
            mana.CurrentMana = math.clamp(currentMana + manaDelta, 0f, maxMp);
            manaRef.ValueRW = mana;
        }
    }
}
