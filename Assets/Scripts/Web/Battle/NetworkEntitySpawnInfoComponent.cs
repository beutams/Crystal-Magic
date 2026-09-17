using Unity.Entities;

namespace Server
{
    /// <summary>
    /// 保留同步实体的生成描述，供运行中重连时重新构造客户端实体。
    /// </summary>
    public sealed class NetworkEntitySpawnInfoComponent : IComponentData
    {
        public NetworkEntitySpawnInfo entityInfo;
    }
}
