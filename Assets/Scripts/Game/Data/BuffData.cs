using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data.Effects;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    public enum BuffCategory
    {
        PropertyModifier = 0,
        Effect = 1,
        SkillModifier = 2,
    }

    [System.Serializable]
    public abstract class BuffData : DataRow
    {
        public string Name;
        public bool CanStack;
        public int MaxStacks = 1;
        public abstract BuffCategory Category { get; }
    }

    public enum PropertyModifierChannel
    {
        [EditorLabel("移速")]
        MoveSpeed = 0,
        [EditorLabel("最大生命")]
        MaxHealth = 1,
        [EditorLabel("防御")]
        Defense = 2,
        [EditorLabel("攻击力")]
        AttackPower = 3,
        [EditorLabel("技能距离")]
        SkillRange = 4,
        [EditorLabel("最大法力")]
        MaxMp = 5,
        [EditorLabel("生命回复")]
        HealthRegen = 6,
        [EditorLabel("法力回复")]
        MpRegen = 7,
        [EditorLabel("动作速度")]
        ActionSpeed = 8,
        [EditorLabel("吟唱速度")]
        ChantSpeed = 9,
        [EditorLabel("水元素强度")]
        WaterPower = 10,
        [EditorLabel("火元素强度")]
        FirePower = 11,
        [EditorLabel("雷元素强度")]
        LightningPower = 12,
        [EditorLabel("风元素强度")]
        WindPower = 13,
    }

    [System.Serializable]
    public struct PropertyModifierEntry
    {
        public PropertyModifierChannel Channel;
        public float Factor;
        public float Bonus;
    }

    public sealed class PropertyModifierSet
    {
        private readonly Dictionary<PropertyModifierChannel, PropertyModifierEntry> _entries = new();

        public void Add(IEnumerable<PropertyModifierEntry> entries, int stacks = 1)
        {
            if (entries == null)
                return;

            foreach (PropertyModifierEntry entry in entries)
                Add(entry, stacks);
        }

        public void Add(PropertyModifierEntry entry, int stacks = 1)
        {
            if (!_entries.TryGetValue(entry.Channel, out PropertyModifierEntry current))
                current.Channel = entry.Channel;

            current.Factor += entry.Factor * stacks;
            current.Bonus += entry.Bonus * stacks;
            _entries[entry.Channel] = current;
        }

        public float GetFactor(PropertyModifierChannel channel)
        {
            return _entries.TryGetValue(channel, out PropertyModifierEntry entry)
                ? 1f + entry.Factor
                : 1f;
        }

        public float GetBonus(PropertyModifierChannel channel)
        {
            return _entries.TryGetValue(channel, out PropertyModifierEntry entry)
                ? entry.Bonus
                : 0f;
        }
    }

    [ReadOnlyData]
    [System.Serializable]
    public class PropertyBuffData : BuffData
    {
        public List<PropertyModifierEntry> PropertyModifiers = new();

        public override BuffCategory Category => BuffCategory.PropertyModifier;
    }

    [ReadOnlyData]
    [System.Serializable]
    public class EffectBuffData : BuffData
    {
        public override BuffCategory Category => BuffCategory.Effect;

        [SerializeReference]
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();
    }

    [ReadOnlyData]
    [System.Serializable]
    public class SkillModifierBuffData : BuffData
    {
        public List<SkillModifierEntry> SkillModifiers = new();

        public override BuffCategory Category => BuffCategory.SkillModifier;
    }
}
