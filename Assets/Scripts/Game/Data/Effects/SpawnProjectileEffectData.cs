using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class SpawnProjectileEffectData : EffectData
    {
        [EditorLabel("逻辑投射物预制体名称")]
        public string ProjectilePrefabName = "Projectile";

        [EditorLabel("投射物视觉预制体名称")]
        public string VisualPrefabName;

        [EditorLabel("投射物视觉缩放")]
        public float VisualScale = 1f;

        [EditorLabel("投射物视觉偏移")]
        public Vector3 VisualOffset;

        [EditorLabel("飞行速度")]
        public float Speed;

        [EditorLabel("最大距离")]
        public float MaxRange;

        [EditorLabel("生成前移距离")]
        public float SpawnOffsetDistance;

        [EditorLabel("Hit Radius")]
        public float HitRadius = 0.75f;

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
            copy.HitRadius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.ProjectileScale, math.max(0.01f, HitRadius));
            copy.VisualScale = ApplyModifierNonNegative(modifiers, SkillModifierChannel.VfxScale, VisualScale);
            copy.OnCollisionEffects = CreateRuntimeCopies(OnCollisionEffects, modifiers, elementComponent);
            copy.OnDestroyEffects = CreateRuntimeCopies(OnDestroyEffects, modifiers, elementComponent);
            return copy;
        }
    }
}
