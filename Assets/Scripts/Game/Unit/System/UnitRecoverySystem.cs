using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
[UpdateAfter(typeof(UnitBuffSystem))]
partial struct UnitRecoverySystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (RefRW<UnitVitalityComponent> vitalityRef in SystemAPI.Query<RefRW<UnitVitalityComponent>>())
        {
            UnitVitalityComponent vitality = vitalityRef.ValueRO;
            float maxHealth = math.max(0f, vitality.RealMaxHealth);
            float currentHealth = math.clamp(vitality.CurrentHealth, 0f, maxHealth);
            if (currentHealth <= 0f)
            {
                vitality.CurrentHealth = 0f;
                vitalityRef.ValueRW = vitality;
                continue;
            }

            float healthDelta = vitality.RealHealthRegenPerSecond * dt;
            vitality.CurrentHealth = math.clamp(currentHealth + healthDelta, 0f, maxHealth);
            vitalityRef.ValueRW = vitality;
        }

        foreach (RefRW<UnitManaComponent> manaRef in SystemAPI.Query<RefRW<UnitManaComponent>>())
        {
            UnitManaComponent mana = manaRef.ValueRO;
            float maxMp = math.max(0f, mana.RealMaxMp);
            float currentMana = math.clamp(mana.CurrentMana, 0f, maxMp);
            float manaDelta = mana.RealMpRegenPerSecond * dt;
            mana.CurrentMana = math.clamp(currentMana + manaDelta, 0f, maxMp);
            manaRef.ValueRW = mana;
        }
    }
}
