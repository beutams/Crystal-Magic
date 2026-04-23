using System.Collections.Generic;
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
        public float ChantDuration;

        /// <summary>后摇时间（秒）</summary>
        public float RecoveryDuration;

        /// <summary>施法过程中是否允许移动</summary>
        public bool CanMoveDuringWindup;
        public bool CanMoveDuringCasting;
        public bool CanMoveDuringRecovery;

        public bool CanMoveWhileCasting
        {
            get => CanMoveDuringCasting;
            set => CanMoveDuringCasting = value;
        }

        /// <summary>施法移动速度倍率（1 = 不降速）</summary>
        public float MoveSpeedMultiplier;

        /// <summary>图标资源路径（相对 Resources/）</summary>
        public string IconPath;

        /// <summary>技能释放条件（所有条件通过才可释放）</summary>
        public List<ConditionConfig> Conditions = new();

        /// <summary>
        /// 效果链，按执行顺序排列
        /// [SerializeReference] 支持多态子类（AreaSearchEffectData / DamageEffectData / PersistentEffectData 等）
        /// 注意：JsonUtility 不识别此特性，编辑器序列化需使用 Newtonsoft.Json 或手写类型分发
        /// </summary>
        [SerializeReference]
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();
    }

    public enum SkillModifierChannel
    {
        MpCost = 0,
        WindupDuration = 1,
        ChantDuration = 2,
        RecoveryDuration = 3,
        MoveSpeedMultiplier = 4,

        Damage = 100,
        FlatDamage = 101,
        CriticalBonus = 102,
        KnockbackForce = 103,
        HitStunSeconds = 104,

        AreaRadius = 200,
        ProjectileSpeed = 300,
        ProjectileRange = 301,
        ProjectileScale = 302,
        EffectDuration = 400,
        TickInterval = 401,
        VfxScale = 500,
        SoundVolume = 600,
        SoundPitch = 601,
        SoundDelay = 602,
        CameraShakeAmplitude = 700,
        CameraShakeDuration = 701,
        CameraShakeFrequency = 702,
        CameraShakeRadius = 703,
    }

    [System.Serializable]
    public struct SkillModifierEntry
    {
        public SkillModifierChannel Channel;
        public float Factor;
        public float Bonus;
    }

    public sealed class SkillModifierSet
    {
        private readonly Dictionary<SkillModifierChannel, SkillModifierEntry> _entries = new();

        public void Add(IEnumerable<SkillModifierEntry> entries, int stacks = 1)
        {
            if (entries == null)
                return;

            foreach (SkillModifierEntry entry in entries)
                Add(entry, stacks);
        }

        public void Add(SkillModifierEntry entry, int stacks = 1)
        {
            if (!_entries.TryGetValue(entry.Channel, out SkillModifierEntry current))
                current.Channel = entry.Channel;

            current.Factor += entry.Factor * stacks;
            current.Bonus += entry.Bonus * stacks;
            _entries[entry.Channel] = current;
        }

        public float GetFactor(SkillModifierChannel channel)
        {
            return _entries.TryGetValue(channel, out SkillModifierEntry entry)
                ? 1f + entry.Factor
                : 1f;
        }

        public float GetBonus(SkillModifierChannel channel)
        {
            return _entries.TryGetValue(channel, out SkillModifierEntry entry)
                ? entry.Bonus
                : 0f;
        }

        public float Apply(SkillModifierChannel channel, float baseValue)
        {
            return baseValue * GetFactor(channel) + GetBonus(channel);
        }
    }

    public sealed class ResolvedSkillData
    {
        public SkillData Source;
        public int Id;
        public string Name;
        public SkillType SkillType;
        public int MpCost;
        public float WindupDuration;
        public float ChantDuration;
        public float RecoveryDuration;
        public bool CanMoveDuringWindup;
        public bool CanMoveDuringCasting;
        public bool CanMoveDuringRecovery;
        public float MoveSpeedMultiplier;
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();

        public bool CanMoveWhileCasting
        {
            get => CanMoveDuringCasting;
            set => CanMoveDuringCasting = value;
        }
    }
}
