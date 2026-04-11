using UnityEngine;
using UnityEngine.EventSystems;

namespace CrystalMagic.Core
{
    /// <summary>
    /// 跨场景持久化 EventSystem
    /// 挂载在 Start 场景的 EventSystem GameObject 上
    /// 其他场景若含有 EventSystem，加载时自动销毁多余的
    /// </summary>
    [RequireComponent(typeof(EventSystem))]
    public class PersistentEventSystem : MonoBehaviour
    {
        private static PersistentEventSystem _instance;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
