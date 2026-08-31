using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;
using UnityEngine;

namespace CrystalMagic.Editor.EffectGraph
{
    public readonly struct EffectGraphTypeInfo
    {
        public EffectGraphTypeInfo(Type type, string displayName, Color color)
        {
            Type = type;
            DisplayName = displayName ?? string.Empty;
            Color = color;
        }

        public Type Type { get; }

        public string DisplayName { get; }

        public Color Color { get; }
    }

    public static class EffectGraphTypeRegistry
    {
        private static readonly EffectGraphTypeInfo[] s_types =
        {
            new(typeof(ApplyBuffEffectData), "Apply Buff", new Color(0.34f, 0.22f, 0.56f)),
            new(typeof(AreaSearchEffectData), "Area Search", new Color(0.14f, 0.38f, 0.60f)),
            new(typeof(ChainSearchEffectData), "Chain Search", new Color(0.18f, 0.42f, 0.74f)),
            new(typeof(ConeSearchEffectData), "Cone Search", new Color(0.20f, 0.50f, 0.70f)),
            new(typeof(DamageEffectData), "Damage", new Color(0.60f, 0.18f, 0.14f)),
            new(typeof(ForwardRectSearchEffectData), "Forward Rect Search", new Color(0.60f, 0.30f, 0.12f)),
            new(typeof(HealEffectData), "Heal", new Color(0.16f, 0.52f, 0.22f)),
            new(typeof(HealthCostEffectData), "Health Cost", new Color(0.42f, 0.16f, 0.16f)),
            new(typeof(FearEffectData), "Fear", new Color(0.42f, 0.24f, 0.12f)),
            new(typeof(KnockbackEffectData), "Knockback", new Color(0.68f, 0.26f, 0.12f)),
            new(typeof(PersistentEffectData), "Persistent", new Color(0.14f, 0.50f, 0.24f)),
            new(typeof(RandomAreaPointEffectData), "Random Area Points", new Color(0.26f, 0.50f, 0.24f)),
            new(typeof(ReadBuffStackEffectData), "Read Buff Stack", new Color(0.22f, 0.42f, 0.64f)),
            new(typeof(RemoveBuffEffectData), "Remove Buff", new Color(0.50f, 0.18f, 0.18f)),
            new(typeof(RestoreManaEffectData), "Restore Mana", new Color(0.14f, 0.46f, 0.60f)),
            new(typeof(SpawnProjectileEffectData), "Spawn Projectile", new Color(0.55f, 0.38f, 0.10f)),
            new(typeof(SpawnSoundEffectData), "Spawn Sound", new Color(0.38f, 0.18f, 0.55f)),
            new(typeof(SpawnUnitEffectData), "Spawn Unit", new Color(0.46f, 0.30f, 0.14f)),
            new(typeof(SpawnFollowVfxEffectData), "Spawn Follow VFX", new Color(0.18f, 0.54f, 0.54f)),
            new(typeof(SpawnVfxEffectData), "Spawn VFX", new Color(0.18f, 0.48f, 0.48f)),
            new(typeof(StunEffectData), "Stun", new Color(0.32f, 0.32f, 0.32f)),
            new(typeof(CameraShakeEffectData), "Camera Shake", new Color(0.58f, 0.42f, 0.12f)),
        };

        private static readonly Dictionary<Type, EffectGraphTypeInfo> s_typeInfoByType = BuildTypeLookup();

        public static IReadOnlyList<EffectGraphTypeInfo> Types => s_types;

        public static bool TryGet(Type type, out EffectGraphTypeInfo typeInfo)
        {
            if (type != null && s_typeInfoByType.TryGetValue(type, out typeInfo))
                return true;

            typeInfo = default;
            return false;
        }

        public static bool TryCreate(Type type, out EffectData effect)
        {
            effect = null;
            if (type == null || type.IsAbstract || !typeof(EffectData).IsAssignableFrom(type))
                return false;

            try
            {
                effect = Activator.CreateInstance(type) as EffectData;
                return effect != null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        public static string GetDisplayName(EffectData effect)
        {
            if (effect == null)
                return "(Missing Effect)";

            return TryGet(effect.GetType(), out EffectGraphTypeInfo typeInfo)
                ? typeInfo.DisplayName
                : EditorLabelUtility.GetLabel(effect.GetType(), effect.GetType().Name);
        }

        public static Color GetColor(EffectData effect)
        {
            return effect != null && TryGet(effect.GetType(), out EffectGraphTypeInfo typeInfo)
                ? typeInfo.Color
                : new Color(0.4f, 0.4f, 0.4f);
        }

        private static Dictionary<Type, EffectGraphTypeInfo> BuildTypeLookup()
        {
            Dictionary<Type, EffectGraphTypeInfo> lookup = new();
            for (int index = 0; index < s_types.Length; index++)
                lookup[s_types[index].Type] = s_types[index];

            return lookup;
        }
    }
}
