using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class SpawnUnitEffectData : EffectData
    {
        [EditorLabel("Unit Name")]
        public string UnitName;

        [EditorLabel("Count")]
        public int Count = 1;

        [EditorLabel("Spawn Radius")]
        public float SpawnRadius = 1f;

        [EditorLabel("Min Spawn Radius")]
        public float MinSpawnRadius;

        [EditorLabel("Center Offset")]
        public Vector3 CenterOffset;

        [EditorLabel("Copy Faction From Caster")]
        public bool CopyFactionFromCaster = true;

        public override EffectData CreateRuntimeCopy(
            SkillModifierSet modifiers,
            float elementBonus = 0f,
            System.Func<EffectData, float> elementBonusResolver = null)
        {
            SpawnUnitEffectData copy = (SpawnUnitEffectData)base.CreateRuntimeCopy(modifiers, elementBonus, elementBonusResolver);
            copy.Count = Unity.Mathematics.math.max(1, Count);
            copy.SpawnRadius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.AreaRadius, SpawnRadius);
            copy.MinSpawnRadius = ApplyModifierNonNegative(modifiers, SkillModifierChannel.AreaRadius, MinSpawnRadius);
            return copy;
        }
    }
}
