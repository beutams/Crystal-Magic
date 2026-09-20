using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    [System.Serializable]
    public class BuffData : DataRow
    {
        public string NameKey;
        public string IconPath;
        public bool CanStack;
        public int MaxStacks = 1;
        public List<PropertyModifierEntry> PropertyModifiers = new();
        public List<SkillModifierEntry> SkillModifiers = new();
        public List<BuffTriggerEntry> TriggerEntries = new();

        [JsonIgnore]
        public string Name => LocalizationComponent.Resolve(NameKey);

        public List<BuffTriggerEntry> CreateEffectiveTriggerEntries()
        {
            return TriggerEntries ?? new List<BuffTriggerEntry>();
        }
    }

    public enum BuffTriggerType : byte
    {
        Tick = 0,
        Hook = 1,
    }

    [System.Serializable]
    public sealed class BuffTriggerEntry
    {
        public BuffTriggerType TriggerType;
        [EditorLabel("触发间隔(秒)")]
        public float TickIntervalSeconds;
        public SkillHookType HookType;
        public bool ConsumeStackOnTrigger;

        [SerializeReference]
        public EffectData[] Effects = System.Array.Empty<EffectData>();
    }

    public enum PropertyModifierChannel
    {
        [EditorLabel("移动速度")]
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
        [EditorLabel("受伤倍率")]
        DamageTakenMultiplier = 14,
    }

    [System.Serializable]
    public struct PropertyModifierEntry
    {
        public PropertyModifierChannel Channel;
        public float Factor;
        public float Bonus;
    }

    public struct PropertyModifierValue
    {
        public float Factor;
        public float Bonus;

        public static PropertyModifierValue Identity => new() { Factor = 1f };

        public readonly float Apply(float baseValue) => baseValue * Factor + Bonus;
    }

    public struct PropertyModifierSet
    {
        private FixedList512Bytes<PropertyModifierAccumulator> _entries;

        public readonly bool IsEmpty => _entries.Length == 0;

        public void Clear() => _entries.Clear();

        public void Add(in PropertyModifierEntry entry, int stacks, float minimumFactor)
        {
            int index = FindIndex(entry.Channel);
            PropertyModifierAccumulator current;
            if (index < 0)
            {
                current = new PropertyModifierAccumulator
                {
                    Channel = entry.Channel,
                    MinimumFactor = minimumFactor,
                };
                index = _entries.Length;
                _entries.Add(current);
            }
            else
            {
                current = _entries[index];
                current.MinimumFactor = math.max(current.MinimumFactor, minimumFactor);
            }

            current.FactorSum += entry.Factor * math.max(1, stacks);
            current.Bonus += entry.Bonus * stacks;
            _entries[index] = current;
        }

        public readonly PropertyModifierValue GetValue(PropertyModifierChannel channel)
        {
            int index = FindIndex(channel);
            if (index < 0)
                return PropertyModifierValue.Identity;

            PropertyModifierAccumulator entry = _entries[index];
            float factor = math.max(0f, 1f + entry.FactorSum);
            return new PropertyModifierValue
            {
                Factor = math.max(entry.MinimumFactor, factor),
                Bonus = entry.Bonus,
            };
        }

        private readonly int FindIndex(PropertyModifierChannel channel)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Channel == channel)
                    return i;
            }

            return -1;
        }

        private struct PropertyModifierAccumulator
        {
            public PropertyModifierChannel Channel;
            public float FactorSum;
            public float Bonus;
            public float MinimumFactor;
        }
    }

    // Legacy serialized subtype aliases. New buff rows use BuffData directly.
    [System.Serializable]
    public class PropertyBuffData : BuffData
    {
    }

    [System.Serializable]
    public class EffectBuffData : BuffData
    {
    }

    [System.Serializable]
    public class SkillModifierBuffData : BuffData
    {
    }

}
