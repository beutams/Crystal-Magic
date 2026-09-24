using Unity.Entities;
using Unity.Physics;

// 观战实体保留角色和网络身份，只暂停战斗职能；恢复在线时还原碰撞体。
public struct BattleSpectatorComponent : IComponentData
{
    public PhysicsCollider Collider;
    public byte HadCollider;
}
