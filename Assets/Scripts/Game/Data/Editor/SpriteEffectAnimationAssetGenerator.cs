using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    internal static class SpriteEffectAnimationAssetGenerator
    {
        private const string EffectAnimationFolder = "Assets/Res/Animation/Effects";
        private const string VfxPrefabFolder = "Assets/Res/Prefab/VFX";
        private const string ProjectilePrefabPath = "Assets/Res/Prefab/Projectile/Projectile.prefab";
        private const string LegacyVfxPrefabPath = "Assets/Res/Prefab/VFX/VFX.prefab";
        private const float FrameRate = 16f;

        [MenuItem("Tools/Data/Generate Fireball Sprite Effects")]
        public static void GenerateFireballSpriteEffects()
        {
            EnsureFolder(EffectAnimationFolder);

            AnimationClip projectileLoop = CreateSpriteClip(
                "Assets/Res/Sprites/UISprites/SoggySocks Fire FX/PNG/proj_fireball_sheet.png",
                $"{EffectAnimationFolder}/FireballProjectileLoop.anim",
                true);
            AnimationClip solarFlashEnter = CreateSpriteClip(
                "Assets/Res/Sprites/UISprites/SoggySocks Fire FX/PNG/fire_solarflash_sheet.png",
                $"{EffectAnimationFolder}/FireballSolarFlashEnter.anim",
                false);

            EnsureFolder(VfxPrefabFolder);
            CreateVisualPrefab("FireballProjectileVisual", null, projectileLoop, null);
            CreateVisualPrefab("FireballSolarFlash", solarFlashEnter, null, null);
            StripProjectileRenderer();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(LegacyVfxPrefabPath) != null)
                AssetDatabase.DeleteAsset(LegacyVfxPrefabPath);

            UnitAnimationFrameLibraryBuilder.RebuildFromSavedProfiles();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static AnimationClip CreateSpriteClip(string spriteSheetPath, string clipPath, bool loop)
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath)
                .OfType<Sprite>()
                .OrderBy(sprite => sprite.rect.y)
                .ThenBy(sprite => sprite.rect.x)
                .ToArray();
            if (sprites.Length == 0)
                throw new System.InvalidOperationException($"No sprites were found in '{spriteSheetPath}'.");

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.frameRate = FrameRate;
            clip.ClearCurves();

            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Length + 1];
            for (int index = 0; index < sprites.Length; index++)
            {
                keys[index] = new ObjectReferenceKeyframe
                {
                    time = index / FrameRate,
                    value = sprites[index],
                };
            }

            keys[^1] = new ObjectReferenceKeyframe
            {
                time = sprites.Length / FrameRate,
                value = sprites[^1],
            };

            AnimationUtility.SetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"),
                keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void CreateVisualPrefab(
            string prefabName,
            AnimationClip enterClip,
            AnimationClip loopClip,
            AnimationClip exitClip)
        {
            AnimationClip firstClip = enterClip ?? loopClip ?? exitClip;
            UnitAnimationFrameTrack firstTrack = BuildPreviewTrack(firstClip);
            GameObject root = new(prefabName);
            try
            {
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = firstTrack?.SampleSprite(0f);
                root.AddComponent<PoolPreset>().Tier = PoolPresetTier.Large;
                SpriteEffectAnimationAuthoring authoring = root.AddComponent<SpriteEffectAnimationAuthoring>();
                authoring.EnterClip = enterClip;
                authoring.LoopClip = loopClip;
                authoring.ExitClip = exitClip;
                PrefabUtility.SaveAsPrefabAsset(root, $"{VfxPrefabFolder}/{prefabName}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static UnitAnimationFrameTrack BuildPreviewTrack(AnimationClip clip)
        {
            if (clip == null)
                return null;

            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys == null || keys.Length == 0)
                return null;

            return new UnitAnimationFrameTrack
            {
                Sprites = keys.Select(key => key.value as Sprite).ToArray(),
                SpriteTimes = keys.Select(key => key.time).ToArray(),
            };
        }

        private static void StripProjectileRenderer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ProjectilePrefabPath);
            try
            {
                MeshRenderer renderer = root.GetComponent<MeshRenderer>();
                if (renderer != null)
                    Object.DestroyImmediate(renderer);

                MeshFilter filter = root.GetComponent<MeshFilter>();
                if (filter != null)
                    Object.DestroyImmediate(filter);

                PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
