using System;
using Unity.Entities;

public class NetworkPlayerAuthoring
{

}
public struct NetworkPlayerComponent : IComponentData
{
    public Guid id;
}
