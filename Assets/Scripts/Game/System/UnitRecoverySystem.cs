using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateBefore(typeof(UnitControlSystem))]
public partial struct UnitRecoverySystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<UnitModifierComponent> modifiers =
            SystemAPI.GetComponentLookup<UnitModifierComponent>(true);
        float deltaTime = SystemAPI.Time.DeltaTime;
        JobHandle inputDependency = state.Dependency;
        JobHandle healthHandle = new UnitHealthRecoveryJob
        {
            DeltaTime = deltaTime,
            Modifiers = modifiers,
        }.ScheduleParallel(inputDependency);
        JobHandle manaHandle = new UnitManaRecoveryJob
        {
            DeltaTime = deltaTime,
            Modifiers = modifiers,
        }.ScheduleParallel(inputDependency);
        state.Dependency = JobHandle.CombineDependencies(healthHandle, manaHandle);
    }
}

[BurstCompile]
[WithNone(typeof(BattleSpectatorComponent))]
public partial struct UnitHealthRecoveryJob : IJobEntity
{
    public float DeltaTime;

    [ReadOnly]
    public ComponentLookup<UnitModifierComponent> Modifiers;

    private void Execute(Entity entity, ref UnitVitalityComponent vitality)
    {
        UnitModifierComponent modifiers = Modifiers.TryGetComponent(entity, out UnitModifierComponent value)
            ? value
            : UnitModifierComponent.CreateIdentity();
        float maxHealth = math.max(
            0f,
            UnitModifierResolver.GetMaxHealth(in vitality, in modifiers));
        float currentHealth = math.clamp(vitality.CurrentHealth, 0f, maxHealth);
        if (currentHealth <= 0f)
        {
            if (vitality.CurrentHealth == 0f)
                return;

            vitality.CurrentHealth = 0f;
            vitality.NetworkDirty = 1;
            return;
        }

        float healthDelta = UnitModifierResolver.GetHealthRegen(in vitality, in modifiers) * DeltaTime;
        float nextHealth = math.clamp(currentHealth + healthDelta, 0f, maxHealth);
        if (nextHealth == vitality.CurrentHealth)
            return;

        vitality.CurrentHealth = nextHealth;
        vitality.NetworkDirty = 1;
    }
}

[BurstCompile]
[WithNone(typeof(BattleSpectatorComponent))]
public partial struct UnitManaRecoveryJob : IJobEntity
{
    public float DeltaTime;

    [ReadOnly]
    public ComponentLookup<UnitModifierComponent> Modifiers;

    private void Execute(Entity entity, ref UnitManaComponent mana)
    {
        UnitModifierComponent modifiers = Modifiers.TryGetComponent(entity, out UnitModifierComponent value)
            ? value
            : UnitModifierComponent.CreateIdentity();
        float maxMp = math.max(0f, UnitModifierResolver.GetMaxMp(in mana, in modifiers));
        float currentMana = math.clamp(mana.CurrentMana, 0f, maxMp);
        float manaDelta = UnitModifierResolver.GetMpRegen(in mana, in modifiers) * DeltaTime;
        float nextMana = math.clamp(currentMana + manaDelta, 0f, maxMp);
        if (nextMana == mana.CurrentMana)
            return;

        mana.CurrentMana = nextMana;
        mana.NetworkDirty = 1;
    }
}
