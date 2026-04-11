using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// UI 子对象引用基类
    /// 右键 Prefab → Generate UIData 重新生成
    /// 命名规则：Panel/Button → Panel_Button，同名兄弟追加 _1/_2
    /// </summary>
    public abstract class UIData
    {
        public abstract void Bind(Transform root);

        /// <summary>
        /// 按路径查找子对象（Transform.Find 封装）
        /// </summary>
        protected static GameObject Find(Transform root, string path)
            => root.Find(path)?.gameObject;

        /// <summary>
        /// 按路径查找同名兄弟中第 index 个（0 = 第一个）
        /// 用于同一父节点下存在多个同名子对象的情况
        /// </summary>
        protected static GameObject FindAt(Transform root, string parentPath, string childName, int index)
        {
            Transform parent = string.IsNullOrEmpty(parentPath) ? root : root.Find(parentPath);
            if (parent == null) return null;

            int count = 0;
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    if (count == index) return child.gameObject;
                    count++;
                }
            }
            return null;
        }
    }
}
