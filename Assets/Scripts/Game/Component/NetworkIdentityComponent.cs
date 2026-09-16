using System;
using Unity.Entities;

public struct NetworkIdentityComponent : IComponentData
{
    public Guid id;
}
