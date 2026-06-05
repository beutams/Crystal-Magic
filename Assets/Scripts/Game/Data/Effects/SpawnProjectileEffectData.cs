using UnityEngine;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using Unity.Mathematics;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class SpawnProjectileEffectData : EffectData
    {
        [EditorLabel("飞行速度")]
        public float Speed;
        [EditorLabel("最大距离")]
        public float MaxRange;
        [EditorLabel("生成前移距离")]
        public float SpawnOffsetDistance;
        [EditorLabel("缩放")]
        public float Scale = 1f;
        [EditorLabel("飞行贴图")]
        public Texture2D FlightTexture;
        [EditorLabel("飞行帧数")]
        public int FlightFrameCount = 16;
        [EditorLabel("销毁贴图")]
        public Texture2D DestroyTexture;
        [EditorLabel("销毁帧数")]
        public int DestroyFrameCount = 16;
        [EditorLabel("可穿透")]
        public bool CanPierce;
        [EditorLabel("到最远距离触发销毁效果")]
        public bool TriggerDestroyEffectsOnMaxRange;

        [EditorLabel("命中效果")]
        [SerializeReference]
        public EffectData[] OnCollisionEffects;

        [EditorLabel("销毁效果")]
        [SerializeReference]
        [JsonProperty("OnDestoryEffects")]
        public EffectData[] OnDestroyEffects;

        public override EffectData CreateRuntimeCopy(
            SkillModifierSet modifiers,
            UnitElementComponent? elementComponent = null)
        {
            SpawnProjectileEffectData copy = (SpawnProjectileEffectData)base.CreateRuntimeCopy(modifiers, elementComponent);
            copy.Speed = ApplyModifierNonNegative(modifiers, SkillModifierChannel.ProjectileSpeed, Speed);
            copy.MaxRange = ApplyModifierNonNegative(modifiers, SkillModifierChannel.ProjectileRange, MaxRange);
            copy.Scale = ApplyModifierNonNegative(modifiers, SkillModifierChannel.ProjectileScale, Scale);
            copy.FlightFrameCount = math.clamp(FlightFrameCount, 1, 16);
            copy.DestroyFrameCount = math.clamp(DestroyFrameCount, 1, 16);
            copy.OnCollisionEffects = CreateRuntimeCopies(OnCollisionEffects, modifiers, elementComponent);
            copy.OnDestroyEffects = CreateRuntimeCopies(OnDestroyEffects, modifiers, elementComponent);
            return copy;
        }
    }
}
