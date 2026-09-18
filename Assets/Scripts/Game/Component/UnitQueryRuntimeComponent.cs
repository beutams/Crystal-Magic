using System;
using Unity.Entities;

public struct UnitQuerySingleton : IComponentData
{
}

public sealed class UnitQueryRuntimeComponent : IComponentData, IDisposable
{
    public readonly UnitQueryGrid UnitGrid = new();
    public readonly UnitQueryGrid InteractableGrid = new();

    public void Dispose()
    {
        UnitGrid.Dispose();
        InteractableGrid.Dispose();
    }
}
