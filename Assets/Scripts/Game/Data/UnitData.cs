using System.Collections.Generic;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Data
{
    /// <summary>
    /// 单位配置表行（合并原 UnitAttributeData）
    /// JSON：Assets/Res/Data/UnitDataTable.json
    /// Name 字段与 GameObject 名称对应，Baker 据此查找
    /// </summary>
    [ReadOnlyData]
    [System.Serializable]
    public class UnitData : DataRow
    {
        // ── 基础信息 ──────────────────────────────────
        public string Name;
        public string Description;
        public string PrefabPath;

        // ── Move ─────────────────────────────────────
        public float BaseMoveSpeed;
        public float BaseMaxAcceleration;

        // ── Vitality（Health + Defense）─────────────
        public float BaseMaxHealth;
        public float BaseDefense;

        // ── Attack ──────────────────────────────────
        public float BaseAttackPower;
        public float BaseSkillRange;

        // ── Mana ────────────────────────────────────
        public float BaseMaxMp;

        // ── AI / 状态机 ──────────────────────────────
        public List<UnitStateConfig> States = new();
    }
    /// <summary>
    /// 单个状态的配置（可序列化，存于 UnitData.States）
    /// StateType 对应 AUnitState 子类名称
    /// </summary>
    [System.Serializable]
    public class UnitStateConfig
    {
        public string StateType = "";
        public List<UnitTransitionConfig> Transitions = new();
    }

    /// <summary>
    /// 一条状态转换规则：从当前状态 → TargetStateType，满足所有 Conditions 时触发
    /// </summary>
    [System.Serializable]
    public class UnitTransitionConfig
    {
        public string TargetStateType = "";
        public List<ConditionConfig> Conditions = new();
    }
}
