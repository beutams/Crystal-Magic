using Unity.Entities;

public struct UnitQuerySingleton : IComponentData
{
}

public sealed class UnitQueryRuntimeComponent : IComponentData
{
    public UnitQueryTree UnitTree = new();
    public UnitQueryTree InteractableTree = new();
}
