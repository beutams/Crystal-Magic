using System;
using Unity.Entities;

public class NetworkIdentityAuthoring
{

}
public struct NetworkIdentityComponent : IComponentData
{
    public Guid id;
}
