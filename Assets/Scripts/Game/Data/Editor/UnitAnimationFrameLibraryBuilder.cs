using System;
using System.Collections.Generic;
using System.IO;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    internal static class UnitAnimationFrameLibraryBuilder
    {
        internal const string AssetPath = "Assets/Res/Data/UnitAnimationFrameLibrary.asset";
        private const string ProfileDataPath = "Assets/Res/Data/UnitAnimationProfileDataTable.json";

        [InitializeOnLoadMethod]
        private static void BuildMissingLibraryAfterDomainReload()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<UnitAnimationFrameLibrary>(AssetPath) != null)
                    return;

                RebuildFromSavedProfiles();
            };
        }

        internal static void RebuildFromSavedProfiles()
        {
            if (!File.Exists(ProfileDataPath))
                return;

            string json = DataFileUtility.ReadJsonText(ProfileDataPath);
            ProfileTable table = JsonConvert.DeserializeObject<ProfileTable>(json);
            if (table?.Rows == null)
                return;

            Rebuild(table.Rows);
            AssetDatabase.SaveAssets();
        }

        internal static void Rebuild(IEnumerable<UnitAnimationProfileData> profiles)
        {
            UnitAnimationFrameLibrary library = AssetDatabase.LoadAssetAtPath<UnitAnimationFrameLibrary>(AssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<UnitAnimationFrameLibrary>();
                AssetDatabase.CreateAsset(library, AssetPath);
            }

            HashSet<string> paths = new(StringComparer.Ordinal);
            foreach (UnitAnimationProfileData profile in profiles)
            {
                if (profile?.Animations == null)
                    continue;

                for (int i = 0; i < profile.Animations.Count; i++)
                {
                    UnitAnimationEntryData entry = profile.Animations[i];
                    if (entry == null)
                        continue;

                    AddPath(paths, entry.FrontClipPath);
                    AddPath(paths, entry.BackClipPath);
                    AddPath(paths, entry.LeftClipPath);
                    AddPath(paths, entry.RightClipPath);
                }
            }

            List<UnitAnimationFrameTrack> tracks = new(paths.Count);
            foreach (string path in paths)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    Debug.LogWarning($"[UnitAnimationFrameLibrary] Missing AnimationClip: {path}");
                    continue;
                }

                tracks.Add(BuildTrack(path, clip));
            }

            library.Tracks = tracks;
            EditorUtility.SetDirty(library);
        }

        private static UnitAnimationFrameTrack BuildTrack(string path, AnimationClip clip)
        {
            EditorCurveBinding spriteBinding = EditorCurveBinding.PPtrCurve(
                string.Empty,
                typeof(SpriteRenderer),
                "m_Sprite");
            ObjectReferenceKeyframe[] spriteKeys = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding) ??
                                                   Array.Empty<ObjectReferenceKeyframe>();

            EditorCurveBinding flipXBinding = EditorCurveBinding.FloatCurve(
                string.Empty,
                typeof(SpriteRenderer),
                "m_FlipX");
            AnimationCurve flipXCurve = AnimationUtility.GetEditorCurve(clip, flipXBinding);
            Keyframe[] flipXKeys = flipXCurve?.keys ?? Array.Empty<Keyframe>();

            Sprite[] sprites = new Sprite[spriteKeys.Length];
            float[] spriteTimes = new float[spriteKeys.Length];
            for (int i = 0; i < spriteKeys.Length; i++)
            {
                sprites[i] = spriteKeys[i].value as Sprite;
                spriteTimes[i] = spriteKeys[i].time;
            }

            float[] flipXTimes = new float[flipXKeys.Length];
            float[] flipXValues = new float[flipXKeys.Length];
            for (int i = 0; i < flipXKeys.Length; i++)
            {
                flipXTimes[i] = flipXKeys[i].time;
                flipXValues[i] = flipXKeys[i].value;
            }

            return new UnitAnimationFrameTrack
            {
                ClipPath = path,
                SourceClip = clip,
                Sprites = sprites,
                SpriteTimes = spriteTimes,
                FlipXTimes = flipXTimes,
                FlipXValues = flipXValues,
                Length = clip.length,
                IsLooping = clip.isLooping,
            };
        }

        private static void AddPath(HashSet<string> paths, string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
                paths.Add(path);
        }

        [Serializable]
        private sealed class ProfileTable
        {
            public List<UnitAnimationProfileData> Rows = new();
        }
    }

    internal sealed class UnitAnimationFrameLibraryPostprocessor : AssetPostprocessor
    {
        private static bool s_rebuildScheduled;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (s_rebuildScheduled || !HasAnimationSourceChange(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths))
                return;

            s_rebuildScheduled = true;
            EditorApplication.delayCall += () =>
            {
                s_rebuildScheduled = false;
                UnitAnimationFrameLibraryBuilder.RebuildFromSavedProfiles();
            };
        }

        private static bool HasAnimationSourceChange(params string[][] pathGroups)
        {
            for (int groupIndex = 0; groupIndex < pathGroups.Length; groupIndex++)
            {
                string[] paths = pathGroups[groupIndex];
                for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
                {
                    string path = paths[pathIndex];
                    if (path == "Assets/Res/Data/UnitAnimationProfileDataTable.json" ||
                        path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
