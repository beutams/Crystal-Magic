using CrystalMagic.Core;
using Unity.Entities;

[UpdateInGroup(typeof(UnitDecisionSystemGroup))]
[UpdateAfter(typeof(BehaviorTreeSystem))]
[UpdateAfter(typeof(UnitControlSystem))]
[UpdateBefore(typeof(UnitSkillSystem))]
public partial class StateScriptSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (UnitStateScriptComponent component in
                 SystemAPI.Query<UnitStateScriptComponent>().WithNone<UnitDeathComponent>())
        {
            if (component == null || !component.IsInitialized || component.IsStoppedForDeath)
                continue;

            for (int i = 0; i < component.Runtimes.Count; i++)
                component.Runtimes[i].Tick(deltaTime);
        }

        foreach (UnitStateScriptComponent component in
                 SystemAPI.Query<UnitStateScriptComponent>().WithAll<UnitDeathComponent>())
        {
            if (component == null || component.IsStoppedForDeath)
                continue;

            for (int i = 0; i < component.Runtimes.Count; i++)
                component.Runtimes[i].StopAllWithoutOutput();

            component.IsStoppedForDeath = true;
        }
    }
}
