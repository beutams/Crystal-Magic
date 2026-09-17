using System.Collections.Generic;
using Unity.Entities;

namespace Server
{
    /// <summary>
    /// Battle Server World 唯一的同步实体生成队列。
    /// 初始化阶段由 ServerBattleManager 一次性取出；运行阶段由帧末收集 System 取出。
    /// </summary>
    public sealed class NetworkEntitySpawnQueueComponent : IComponentData
    {
        public readonly List<NetworkEntitySpawnInfo> entityInfos = new();
    }
}
