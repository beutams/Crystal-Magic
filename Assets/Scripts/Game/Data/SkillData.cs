using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data.Effects;
using UnityEngine;
using Unity.Mathematics;

namespace CrystalMagic.Game.Data
{
    /// <summary>技能类型</summary>
    public enum SkillType
    {
        PositionSkill = 0,
        SelfSkill = 1,
    }

    /// <summary>
    /// 技能配置表行
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
        public bool CanMoveWhileCasting;

        /// <summary>施法移动速度倍率（1 = 不降速）</summary>
        public float MoveSpeedMultiplier;

        /// <summary>图标资源路径（相对 Resources/）</summary>
        public string IconPath;

        /// <summary>技能释放条件（所有条件通过才可释放）</summary>
        public List<ConditionConfig> Conditions = new();

        /// <summary>
        /// 效果链，按执行顺序排列
        /// </summary>
        [SerializeReference]
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();
        public List<SkillFollowupEffectData> FollowupEffects = new();
    }

    public enum SkillModifierChannel
    {
        [EditorLabel("法力消耗")]
        MpCost = 0,
        [EditorLabel("动作速度")]
        ActionSpeed = 1,
        [EditorLabel("吟唱速度")]
        ChantSpeed = 2,
        [EditorLabel("保留")]
        Reserved = 3,
        [EditorLabel("施法移动速度倍率")]
        MoveSpeedMultiplier = 4,

        [EditorLabel("伤害倍率")]
        Damage = 100,
        [EditorLabel("额外伤害")]
        FlatDamage = 101,
        [EditorLabel("击退力度")]
        KnockbackForce = 103,
        [EditorLabel("受击硬直")]
        HitStunSeconds = 104,
        [EditorLabel("治疗倍率")]
        Heal = 105,
        [EditorLabel("额外治疗")]
        FlatHeal = 106,
        [EditorLabel("回蓝倍率")]
        ManaRestore = 107,
        [EditorLabel("额外回蓝")]
        FlatManaRestore = 108,

        [EditorLabel("范围半径")]
        AreaRadius = 200,
        [EditorLabel("投射物速度")]
        ProjectileSpeed = 300,
        [EditorLabel("投射物距离")]
        ProjectileRange = 301,
        [EditorLabel("投射物缩放")]
        ProjectileScale = 302,
        [EditorLabel("效果时长")]
        EffectDuration = 400,
        [EditorLabel("触发间隔")]
        TickInterval = 401,
        [EditorLabel("Buff 时长")]
        BuffDuration = 402,
        [EditorLabel("特效缩放")]
        VfxScale = 500,
        [EditorLabel("音量")]
        SoundVolume = 600,
        [EditorLabel("音调")]
        SoundPitch = 601,
        [EditorLabel("音效延迟")]
        SoundDelay = 602,
        [EditorLabel("震屏振幅")]
        CameraShakeAmplitude = 700,
        [EditorLabel("震屏时长")]
        CameraShakeDuration = 701,
        [EditorLabel("震屏频率")]
        CameraShakeFrequency = 702,
        [EditorLabel("震屏半径")]
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
        private readonly Dictionary<SkillModifierChannel, SkillModifierAccumulator> _entries = new();

        public void Add(IEnumerable<SkillModifierEntry> entries, int stacks = 1)
        {
            if (entries == null)
                return;

            foreach (SkillModifierEntry entry in entries)
                Add(entry, stacks);
        }

        public void Add(SkillModifierEntry entry, int stacks = 1)
        {
            if (!_entries.TryGetValue(entry.Channel, out SkillModifierAccumulator current))
            {
                current.Channel = entry.Channel;
                current.FactorMultiplier = 1f;
            }

            current.FactorMultiplier *= math.pow(math.max(0f, 1f + entry.Factor), math.max(1, stacks));
            current.Bonus += entry.Bonus * stacks;
            _entries[entry.Channel] = current;
        }

        public float GetFactor(SkillModifierChannel channel)
        {
            return _entries.TryGetValue(channel, out SkillModifierAccumulator entry)
                ? math.max(GetMinimumFactor(channel), entry.FactorMultiplier)
                : 1f;
        }

        public float GetBonus(SkillModifierChannel channel)
        {
            return _entries.TryGetValue(channel, out SkillModifierAccumulator entry)
                ? entry.Bonus
                : 0f;
        }

        public float Apply(SkillModifierChannel channel, float baseValue)
        {
            return baseValue * GetFactor(channel) + GetBonus(channel);
        }

        public float GetActionSpeedValue(float baseValue)
        {
            return math.clamp(Apply(SkillModifierChannel.ActionSpeed, baseValue), -100f, 100f);
        }

        public float GetChantSpeedValue(float baseValue)
        {
            return math.clamp(Apply(SkillModifierChannel.ChantSpeed, baseValue), -100f, 100f);
        }

        public float GetMoveSpeedMultiplier()
        {
            return math.max(0f, Apply(SkillModifierChannel.MoveSpeedMultiplier, 1f));
        }

        public void Add(SkillModifierSet other)
        {
            if (other == null)
                return;

            foreach (SkillModifierAccumulator entry in other._entries.Values)
            {
                if (!_entries.TryGetValue(entry.Channel, out SkillModifierAccumulator current))
                {
                    current.Channel = entry.Channel;
                    current.FactorMultiplier = 1f;
                }

                current.FactorMultiplier *= entry.FactorMultiplier;
                current.Bonus += entry.Bonus;
                _entries[entry.Channel] = current;
            }
        }

        public SkillModifierSet Clone()
        {
            SkillModifierSet clone = new();
            clone.Add(this);
            return clone;
        }

        private static float GetMinimumFactor(SkillModifierChannel channel)
        {
            return ConfigComponent.Instance.Get<ModifierConfig>().GetSkillModifierMinimumFactor(channel);
        }

        private struct SkillModifierAccumulator
        {
            public SkillModifierChannel Channel;
            public float FactorMultiplier;
            public float Bonus;
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
        public bool CanMoveWhileCasting;
        public float MoveSpeedMultiplier;
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();
    }
}
