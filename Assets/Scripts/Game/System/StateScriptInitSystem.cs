using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
[UpdateAfter(typeof(UnitSourceDispatcherSystem))]
public partial class StateScriptInitSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (!UnitSourceDispatcherSystem.TryGet(EntityManager, out UnitSourceDispatcher sourceDispatcher))
            return;

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

            StateScriptData data = DataComponent.Instance.Find<StateScriptData>(row => row.Id == component.UnitDataId);
            if (data == null)
            {
                component.InitializationError = $"StateScriptData not found for UnitData.Id: {component.UnitDataId}";
                component.IsInitialized = true;
                continue;
            }

            UnitSourceResolver sources = new(entity);
            sources.Update(entity, UnitVariableSource.GetOther(EntityManager, entity), in sourceDispatcher);
            data.EnsureValid();
            for (int i = 0; i < data.Graphs.Count; i++)
            {
                StateScriptRuntime runtime = StateScriptRuntimeBuilder.Build(
                    data.Graphs[i], entity, EntityManager, sources, out string error);
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
