using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Config
{
    [Serializable]
    public struct PropertyModifierMinimumFactorEntry
    {
        public PropertyModifierChannel Channel;
        public float MinimumFactor;
    }

    [Serializable]
    public struct SkillModifierMinimumFactorEntry
    {
        public SkillModifierChannel Channel;
        public float MinimumFactor;
    }

    [Serializable]
    [GameConfig]
    [EditorLabel("倍率保底配置")]
    public class ModifierConfig
    {
        [EditorLabel("属性倍率保底")]
        public List<PropertyModifierMinimumFactorEntry> PropertyModifierMinimumFactors = CreateDefaultPropertyModifierMinimumFactors();

        [EditorLabel("技能倍率保底")]
        public List<SkillModifierMinimumFactorEntry> SkillModifierMinimumFactors = CreateDefaultSkillModifierMinimumFactors();

        public float GetPropertyModifierMinimumFactor(PropertyModifierChannel channel)
        {
            if (PropertyModifierMinimumFactors != null)
            {
                for (int i = 0; i < PropertyModifierMinimumFactors.Count; i++)
                {
                    if (PropertyModifierMinimumFactors[i].Channel == channel)
                        return PropertyModifierMinimumFactors[i].MinimumFactor;
                }
            }

            return 0f;
        }

        public float GetSkillModifierMinimumFactor(SkillModifierChannel channel)
        {
            if (SkillModifierMinimumFactors != null)
            {
                for (int i = 0; i < SkillModifierMinimumFactors.Count; i++)
                {
                    if (SkillModifierMinimumFactors[i].Channel == channel)
                        return SkillModifierMinimumFactors[i].MinimumFactor;
                }
            }

            return 0f;
        }

        private static List<PropertyModifierMinimumFactorEntry> CreateDefaultPropertyModifierMinimumFactors()
        {
            return new List<PropertyModifierMinimumFactorEntry>
            {
                new() { Channel = PropertyModifierChannel.MoveSpeed, MinimumFactor = 0.3f },
                new() { Channel = PropertyModifierChannel.MaxHealth, MinimumFactor = 0.2f },
                new() { Channel = PropertyModifierChannel.Defense, MinimumFactor = 0.2f },
                new() { Channel = PropertyModifierChannel.AttackPower, MinimumFactor = 0.2f },
                new() { Channel = PropertyModifierChannel.SkillRange, MinimumFactor = 0.5f },
                new() { Channel = PropertyModifierChannel.MaxMp, MinimumFactor = 0.2f },
                new() { Channel = PropertyModifierChannel.HealthRegen, MinimumFactor = 0f },
                new() { Channel = PropertyModifierChannel.MpRegen, MinimumFactor = 0f },
                new() { Channel = PropertyModifierChannel.ActionSpeed, MinimumFactor = 0.3f },
                new() { Channel = PropertyModifierChannel.ChantSpeed, MinimumFactor = 0.3f },
                new() { Channel = PropertyModifierChannel.WaterPower, MinimumFactor = 0f },
                new() { Channel = PropertyModifierChannel.FirePower, MinimumFactor = 0f },
                new() { Channel = PropertyModifierChannel.LightningPower, MinimumFactor = 0f },
                new() { Channel = PropertyModifierChannel.WindPower, MinimumFactor = 0f },
                new() { Channel = PropertyModifierChannel.DamageTakenMultiplier, MinimumFactor = 0f },
            };
        }

        private static List<SkillModifierMinimumFactorEntry> CreateDefaultSkillModifierMinimumFactors()
        {
            return new List<SkillModifierMinimumFactorEntry>
            {
                new() { Channel = SkillModifierChannel.MpCost, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.ActionSpeed, MinimumFactor = 0.3f },
                new() { Channel = SkillModifierChannel.ChantSpeed, MinimumFactor = 0.3f },
                new() { Channel = SkillModifierChannel.Reserved, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.MoveSpeedMultiplier, MinimumFactor = 0.3f },
                new() { Channel = SkillModifierChannel.Damage, MinimumFactor = 0.2f },
                new() { Channel = SkillModifierChannel.FlatDamage, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.KnockbackForce, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.HitStunSeconds, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.Heal, MinimumFactor = 0.2f },
                new() { Channel = SkillModifierChannel.FlatHeal, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.ManaRestore, MinimumFactor = 0.2f },
                new() { Channel = SkillModifierChannel.FlatManaRestore, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.AreaRadius, MinimumFactor = 0.5f },
                new() { Channel = SkillModifierChannel.ProjectileSpeed, MinimumFactor = 0.3f },
                new() { Channel = SkillModifierChannel.ProjectileRange, MinimumFactor = 0.3f },
                new() { Channel = SkillModifierChannel.ProjectileScale, MinimumFactor = 0.3f },
                new() { Channel = SkillModifierChannel.EffectDuration, MinimumFactor = 0.2f },
                new() { Channel = SkillModifierChannel.TickInterval, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.BuffDuration, MinimumFactor = 0.2f },
                new() { Channel = SkillModifierChannel.VfxScale, MinimumFactor = 0.2f },
                new() { Channel = SkillModifierChannel.SoundVolume, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.SoundPitch, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.SoundDelay, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.CameraShakeAmplitude, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.CameraShakeDuration, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.CameraShakeFrequency, MinimumFactor = 0f },
                new() { Channel = SkillModifierChannel.CameraShakeRadius, MinimumFactor = 0f },
            };
        }
    }
}
