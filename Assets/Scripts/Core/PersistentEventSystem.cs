using UnityEngine;
using UnityEngine.EventSystems;

namespace CrystalMagic.Core
{
    /// <summary>
    /// 跨场景持久化的 EventSystem。
    /// 场景中如果重复创建，会保留首个实例并销毁后续实例。
    /// </summary>
    [RequireComponent(typeof(EventSystem))]
    public class PersistentEventSystem : PersistentSingleton<PersistentEventSystem>
    {
    }
}
