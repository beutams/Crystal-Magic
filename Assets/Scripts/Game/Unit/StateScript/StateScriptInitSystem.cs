using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateAfter(typeof(UnitSourceInitializationSystem))]
public partial class StateScriptInitSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach ((UnitStateScriptComponent component, Entity entity) in
                 SystemAPI.Query<UnitStateScriptComponent>().WithEntityAccess())
        {
            if (component == null || component.IsInitialized)
                continue;

            component.Runtimes.Clear();
            component.InitializationError = string.Empty;
            if (component.UnitDataId < 0)
            {
                component.InitializationError = "UnitStateScriptAuthoring could not resolve UnitData.Id.";
                component.IsInitialized = true;
                continue;
            }

            if (!EntityManager.HasComponent<UnitSourceRuntimeComponent>(entity))
                continue;

            StateScriptData data = DataComponent.Instance.Find<StateScriptData>(row => row.UnitDataId == component.UnitDataId);
            if (data == null)
            {
                component.InitializationError = $"StateScriptData not found for UnitData.Id: {component.UnitDataId}";
                component.IsInitialized = true;
                continue;
            }

            UnitSourceRuntimeComponent sourceRuntime = EntityManager.GetComponentObject<UnitSourceRuntimeComponent>(entity);
            if (sourceRuntime?.Table == null)
                continue;

            data.EnsureValid();
            for (int i = 0; i < data.Graphs.Count; i++)
            {
                StateScriptRuntime runtime = StateScriptRuntimeBuilder.Build(
                    data.Graphs[i], entity, EntityManager, sourceRuntime.Table, out string error);
                if (runtime == null)
                {
                    component.InitializationError = error;
                    Debug.LogWarning($"[StateScriptInit] UnitData.Id={component.UnitDataId}: {error}");
                    continue;
                }

                component.Runtimes.Add(runtime);
                runtime.Start();
            }

            component.IsInitialized = true;
        }
    }
}
