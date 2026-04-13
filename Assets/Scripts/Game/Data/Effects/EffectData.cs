using System.Collections.Generic;

namespace CrystalMagic.Game.Data.Effects
{
    /// <summary>
    /// 效果配置数据基类
    /// 子类只存数据字段，不含任何执行逻辑
    /// </summary>
    [System.Serializable]
    public abstract class EffectData
    {
        /// <summary>效果释放条件（所有条件通过才执行该效果）</summary>
        public List<ConditionConfig> Conditions = new();
    }
}

