using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateBefore(typeof(UnitControlSystem))]
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
                if (vitality.CurrentHealth != 0f)
                {
                    vitality.CurrentHealth = 0f;
                    vitality.NetworkDirty = 1;
                    vitalityRef.ValueRW = vitality;
                }
                continue;
            }

            float healthDelta = UnitModifierResolver.GetHealthRegen(EntityManager, entity) * deltaTime;
            float nextHealth = math.clamp(currentHealth + healthDelta, 0f, maxHealth);
            if (nextHealth != vitality.CurrentHealth)
            {
                vitality.CurrentHealth = nextHealth;
                vitality.NetworkDirty = 1;
                vitalityRef.ValueRW = vitality;
            }
        }

        foreach ((RefRW<UnitManaComponent> manaRef, Entity entity) in
                 SystemAPI.Query<RefRW<UnitManaComponent>>().WithEntityAccess())
        {
            UnitManaComponent mana = manaRef.ValueRO;
            float maxMp = math.max(0f, UnitModifierResolver.GetMaxMp(EntityManager, entity));
            float currentMana = math.clamp(mana.CurrentMana, 0f, maxMp);
            float manaDelta = UnitModifierResolver.GetMpRegen(EntityManager, entity) * deltaTime;
            float nextMana = math.clamp(currentMana + manaDelta, 0f, maxMp);
            if (nextMana != mana.CurrentMana)
            {
                mana.CurrentMana = nextMana;
                mana.NetworkDirty = 1;
                manaRef.ValueRW = mana;
            }
        }
    }
}
