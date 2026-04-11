using CrystalMagic.Core;
using CrystalMagic.Game.Data.Effects;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    /// <summary>技能类型</summary>
    public enum SkillType
    {
        PositionSkill = 0,
    }

    /// <summary>
    /// 技能配置表行
    /// JSON：Assets/Res/Data/SkillDataTable.json
    /// </summary>
    [System.Serializable]
    [ReadOnlyData]
    public class SkillData : DataRow
    {
        /// <summary>技能名称</summary>
        public string Name;

        /// <summary>技能描述</summary>
        public string Description;

        /// <summary>技能类型</summary>
        public SkillType SkillType;

        /// <summary>释放消耗的 MP</summary>
        public int MpCost;

        /// <summary>前摇时间（秒）</summary>
        public float WindupDuration;

        /// <summary>后摇时间（秒）</summary>
        public float RecoveryDuration;

        /// <summary>施法过程中是否允许移动</summary>
        public bool CanMoveWhileCasting;

        /// <summary>施法移动速度倍率（1 = 不降速）</summary>
        public float MoveSpeedMultiplier;

        /// <summary>图标资源路径（相对 Resources/）</summary>
        public string IconPath;

        /// <summary>
        /// 效果链，按执行顺序排列
        /// [SerializeReference] 支持多态子类（AreaSearchEffectData / DamageEffectData / PersistentEffectData 等）
        /// 注意：JsonUtility 不识别此特性，编辑器序列化需使用 Newtonsoft.Json 或手写类型分发
        /// </summary>
        [SerializeReference]
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();
    }
}
