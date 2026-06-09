using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using Unity.Mathematics;
using UnityEngine;

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

        [EditorLabel("宽度")]
        public float Width = 1f;

        [EditorLabel("高度")]
        public float Height = 1f;

        [EditorLabel("飞行贴图")]
        public Texture2D FlightTexture;

        [EditorLabel("飞行列数")]
        public int FlightGridColumns = 4;

        [EditorLabel("飞行行数")]
        public int FlightGridRows = 4;

        [EditorLabel("飞行总帧数")]
        public int FlightFrameCount = 16;

        [EditorLabel("飞行每秒帧数")]
        public float FlightFramesPerSecond = 16f;

        [EditorLabel("销毁贴图")]
        public Texture2D DestroyTexture;

        [EditorLabel("销毁列数")]
        public int DestroyGridColumns = 4;

        [EditorLabel("销毁行数")]
        public int DestroyGridRows = 4;

        [EditorLabel("销毁总帧数")]
        public int DestroyFrameCount = 16;

        [EditorLabel("销毁每秒帧数")]
        public float DestroyFramesPerSecond = 16f;

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
            copy.Width = ApplyModifierNonNegative(modifiers, SkillModifierChannel.ProjectileScale, Width > 0f ? Width : Scale);
            copy.Height = ApplyModifierNonNegative(modifiers, SkillModifierChannel.ProjectileScale, Height > 0f ? Height : Scale);
            copy.FlightGridColumns = math.max(1, FlightGridColumns);
            copy.FlightGridRows = math.max(1, FlightGridRows);
            copy.FlightFrameCount = math.clamp(FlightFrameCount, 1, copy.FlightGridColumns * copy.FlightGridRows);
            copy.FlightFramesPerSecond = math.max(0.01f, FlightFramesPerSecond);
            copy.DestroyGridColumns = math.max(1, DestroyGridColumns);
            copy.DestroyGridRows = math.max(1, DestroyGridRows);
            copy.DestroyFrameCount = math.clamp(DestroyFrameCount, 1, copy.DestroyGridColumns * copy.DestroyGridRows);
            copy.DestroyFramesPerSecond = math.max(0.01f, DestroyFramesPerSecond);
            copy.OnCollisionEffects = CreateRuntimeCopies(OnCollisionEffects, modifiers, elementComponent);
            copy.OnDestroyEffects = CreateRuntimeCopies(OnDestroyEffects, modifiers, elementComponent);
            return copy;
        }
    }
}
