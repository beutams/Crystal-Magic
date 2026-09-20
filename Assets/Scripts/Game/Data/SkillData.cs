using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data.Effects;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    [System.Serializable]
    public class SkillData : DataRow
    {
        public string NameKey;
        public bool IsMonsterSkill;
        public string DescriptionKey;
        public string RuntimeType;
        public SkillInputType InputType;
        public int MpCost;
        public float ChantDuration;
        public float CastingMoveMultiplier = 1f;
        public string IconPath;
        public List<ConditionConfig> Conditions = new();

        [SerializeReference]
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();

        [JsonIgnore]
        public string DisplayName => LocalizationComponent.Resolve(NameKey);

        [JsonIgnore]
        public string Description => LocalizationComponent.Resolve(DescriptionKey);
        public string EffectiveRuntimeType => GetEffectiveRuntimeType(RuntimeType);

        public static string GetEffectiveRuntimeType(string runtimeType)
        {
            return runtimeType ?? string.Empty;
        }
    }

    public enum SkillInputType
    {
        None = 0,
        Self = 1,
        MousePosition = 2,
    }

    public enum SkillModifierChannel
    {
        [EditorLabel("MP Cost")]
        MpCost = 0,
        [EditorLabel("Reserved")]
        Reserved = 3,

        [EditorLabel("Damage Multiplier")]
        Damage = 100,
        [EditorLabel("Flat Damage")]
        FlatDamage = 101,
        [EditorLabel("Attribute Power")]
        AttributePower = 102,
        [EditorLabel("Knockback Force")]
        KnockbackForce = 103,
        [EditorLabel("Hit Stun Seconds")]
        HitStunSeconds = 104,
        [EditorLabel("Heal Multiplier")]
        Heal = 105,
        [EditorLabel("Flat Heal")]
        FlatHeal = 106,
        [EditorLabel("Mana Restore Multiplier")]
        ManaRestore = 107,
        [EditorLabel("Flat Mana Restore")]
        FlatManaRestore = 108,

        [EditorLabel("Area Radius")]
        AreaRadius = 200,
        [EditorLabel("Projectile Speed")]
        ProjectileSpeed = 300,
        [EditorLabel("Projectile Range")]
        ProjectileRange = 301,
        [EditorLabel("Projectile Scale")]
        ProjectileScale = 302,
        [EditorLabel("Effect Duration")]
        EffectDuration = 400,
        [EditorLabel("Tick Interval")]
        TickInterval = 401,
        [EditorLabel("Buff Duration")]
        BuffDuration = 402,
        [EditorLabel("VFX Scale")]
        VfxScale = 500,
        [EditorLabel("Sound Volume")]
        SoundVolume = 600,
        [EditorLabel("Sound Pitch")]
        SoundPitch = 601,
        [EditorLabel("Sound Delay")]
        SoundDelay = 602,
        [EditorLabel("Camera Shake Amplitude")]
        CameraShakeAmplitude = 700,
        [EditorLabel("Camera Shake Duration")]
        CameraShakeDuration = 701,
        [EditorLabel("Camera Shake Frequency")]
        CameraShakeFrequency = 702,
        [EditorLabel("Camera Shake Radius")]
        CameraShakeRadius = 703,
    }

    [System.Serializable]
    public struct SkillModifierEntry
    {
        public SkillModifierChannel Channel;
        public float Factor;
        public float Bonus;
    }

    public struct SkillModifierSet
    {
        private FixedList512Bytes<SkillModifierAccumulator> _entries;

        public readonly bool IsEmpty => _entries.Length == 0;

        public void Clear() => _entries.Clear();

        public void Add(IEnumerable<SkillModifierEntry> entries, int stacks = 1)
        {
            if (entries == null)
                return;

            foreach (SkillModifierEntry entry in entries)
                Add(entry, stacks);
        }

        public void Add(SkillModifierEntry entry, int stacks = 1)
        {
            float minimumFactor = ConfigComponent.Instance.Get<ModifierConfig>()
                .GetSkillModifierMinimumFactor(entry.Channel);
            Add(in entry, stacks, minimumFactor);
        }

        public void Add(in SkillModifierEntry entry, int stacks, float minimumFactor)
        {
            int index = FindIndex(entry.Channel);
            SkillModifierAccumulator current;
            if (index < 0)
            {
                current = new SkillModifierAccumulator
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
            current.Bonus += entry.Bonus * math.max(1, stacks);
            _entries[index] = current;
        }

        public readonly float GetFactor(SkillModifierChannel channel)
        {
            int index = FindIndex(channel);
            if (index < 0)
                return 1f;

            SkillModifierAccumulator entry = _entries[index];
            float factor = math.max(0f, 1f + entry.FactorSum);
            return math.max(entry.MinimumFactor, factor);
        }

        public readonly float GetBonus(SkillModifierChannel channel)
        {
            int index = FindIndex(channel);
            return index < 0 ? 0f : _entries[index].Bonus;
        }

        public readonly float Apply(SkillModifierChannel channel, float baseValue)
        {
            return baseValue * GetFactor(channel) + GetBonus(channel);
        }

        public readonly float GetAttributePowerValue()
        {
            return Apply(SkillModifierChannel.AttributePower, 0f);
        }

        public void Add(in SkillModifierSet other)
        {
            for (int i = 0; i < other._entries.Length; i++)
            {
                SkillModifierAccumulator entry = other._entries[i];
                int index = FindIndex(entry.Channel);
                SkillModifierAccumulator current;
                if (index < 0)
                {
                    current = new SkillModifierAccumulator
                    {
                        Channel = entry.Channel,
                        MinimumFactor = entry.MinimumFactor,
                    };
                    index = _entries.Length;
                    _entries.Add(current);
                }
                else
                {
                    current = _entries[index];
                    current.MinimumFactor = math.max(current.MinimumFactor, entry.MinimumFactor);
                }

                current.FactorSum += entry.FactorSum;
                current.Bonus += entry.Bonus;
                _entries[index] = current;
            }
        }

        public readonly SkillModifierSet Clone()
        {
            return this;
        }

        private readonly int FindIndex(SkillModifierChannel channel)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Channel == channel)
                    return i;
            }

            return -1;
        }

        private struct SkillModifierAccumulator
        {
            public SkillModifierChannel Channel;
            public float FactorSum;
            public float Bonus;
            public float MinimumFactor;
        }
    }

    public static class SkillModifierChannelUtility
    {
        private static readonly SkillModifierChannel[] EditableChannels = CreateEditableChannels();
        private static readonly string[] EditableChannelDisplayNames = CreateEditableDisplayNames();

        public static SkillModifierChannel[] GetEditableChannels()
        {
            SkillModifierChannel[] copy = new SkillModifierChannel[EditableChannels.Length];
            System.Array.Copy(EditableChannels, copy, EditableChannels.Length);
            return copy;
        }

        public static string[] GetEditableDisplayNames()
        {
            string[] copy = new string[EditableChannelDisplayNames.Length];
            System.Array.Copy(EditableChannelDisplayNames, copy, EditableChannelDisplayNames.Length);
            return copy;
        }

        public static bool IsInternalChannel(SkillModifierChannel channel)
        {
            return channel == SkillModifierChannel.AttributePower;
        }

        private static SkillModifierChannel[] CreateEditableChannels()
        {
            SkillModifierChannel[] allChannels = (SkillModifierChannel[])System.Enum.GetValues(typeof(SkillModifierChannel));
            List<SkillModifierChannel> channels = new(allChannels.Length);
            for (int i = 0; i < allChannels.Length; i++)
            {
                if (IsInternalChannel(allChannels[i]))
                    continue;

                channels.Add(allChannels[i]);
            }

            return channels.ToArray();
        }

        private static string[] CreateEditableDisplayNames()
        {
            string[] displayNames = new string[EditableChannels.Length];
            for (int i = 0; i < EditableChannels.Length; i++)
                displayNames[i] = EditorLabelUtility.GetEnumValueLabel(EditableChannels[i]);

            return displayNames;
        }
    }

    public sealed class ResolvedSkillData
    {
        public SkillData Source;
        public int Id;
        public string Name;
        public string RuntimeType;
        public int MpCost;
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();
    }
}
