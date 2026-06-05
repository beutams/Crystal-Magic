using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data.Effects;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    [System.Serializable]
    [ReadOnlyData]
    public class SkillData : DataRow
    {
        public string Name;
        public bool IsMonsterSkill;
        public string Description;
        public string RuntimeType;
        public int MpCost;
        public float WindupDuration;
        public float ChantDuration;
        public float RecoveryDuration;
        public bool CanMoveWhileCasting;
        public float MoveSpeedMultiplier;
        public string IconPath;
        public List<ConditionConfig> Conditions = new();

        [SerializeReference]
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();

        public List<SkillCastTaskData> CastTasks = new();
        public List<SkillFollowupEffectData> FollowupEffects = new();

        public string DisplayName => Name ?? string.Empty;
        public string EffectiveRuntimeType => GetEffectiveRuntimeType(RuntimeType);

        public static string GetEffectiveRuntimeType(string runtimeType)
        {
            return runtimeType ?? string.Empty;
        }
    }

    public enum SkillModifierChannel
    {
        [EditorLabel("MP Cost")]
        MpCost = 0,
        [EditorLabel("Action Speed")]
        ActionSpeed = 1,
        [EditorLabel("Chant Speed")]
        ChantSpeed = 2,
        [EditorLabel("Reserved")]
        Reserved = 3,
        [EditorLabel("Cast Move Speed Multiplier")]
        MoveSpeedMultiplier = 4,

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
                current.FactorSum = 0f;
            }

            current.FactorSum += entry.Factor * math.max(1, stacks);
            current.Bonus += entry.Bonus * math.max(1, stacks);
            _entries[entry.Channel] = current;
        }

        public float GetFactor(SkillModifierChannel channel)
        {
            if (!_entries.TryGetValue(channel, out SkillModifierAccumulator entry))
                return 1f;

            float factor = math.max(0f, 1f + entry.FactorSum);
            return math.max(GetMinimumFactor(channel), factor);
        }

        public float GetBonus(SkillModifierChannel channel)
        {
            if (!_entries.TryGetValue(channel, out SkillModifierAccumulator entry))
                return 0f;

            return entry.Bonus;
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

        public float GetAttributePowerValue()
        {
            return Apply(SkillModifierChannel.AttributePower, 0f);
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
                    current.FactorSum = 0f;
                }

                current.FactorSum += entry.FactorSum;
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
            public float FactorSum;
            public float Bonus;
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
        public float WindupDuration;
        public float ChantDuration;
        public float RecoveryDuration;
        public bool CanMoveWhileCasting;
        public float MoveSpeedMultiplier;
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();
    }
}
