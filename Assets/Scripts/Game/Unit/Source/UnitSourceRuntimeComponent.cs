using Unity.Entities;

public sealed class UnitSourceRuntimeComponent : IComponentData
{
    public UnitSourceAccessTable Table = new();
}
