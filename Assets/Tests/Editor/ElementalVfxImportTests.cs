using System.IO;
using System.Linq;
using CrystalMagic.Game.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ElementalVfxImportTests
{
    [Test]
    public void ImportedElementalPrefabsHaveUsableSpritesAndAnimationTracks()
    {
        var library = ScriptableObject.CreateInstance<UnitAnimationFrameLibrary>();
        int count = 0;
        try
        {
            foreach (string path in Directory.GetFiles("Assets/Res/Prefab/VFX", "*.prefab").OrderBy(p => p))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("Water_") && !name.StartsWith("Lightning_") && !name.StartsWith("Earth_"))
                    continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                Assert.That(prefab, Is.Not.Null, path);
                var renderer = prefab.GetComponent<SpriteRenderer>();
                Assert.That(renderer, Is.Not.Null, path);
                Assert.That(renderer.sprite, Is.Not.Null, path);
                var authoring = prefab.GetComponent<SpriteEffectAnimationAuthoring>();
                Assert.That(authoring, Is.Not.Null, path);
                Assert.That(authoring.LoopClip, Is.Not.Null, path);
                var clip = authoring.LoopClip;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip,
                    EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"));
                Assert.That(keys, Is.Not.Empty, path);
                Assert.That(keys.All(k => k.value is Sprite), Is.True, path + " missing sprite frame");
                foreach (Sprite sprite in keys.Select(k => (Sprite)k.value))
                {
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16), path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                    Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                    Assert.That(importer.mipmapEnabled, Is.False, path);
                }
                library.Tracks.Add(new UnitAnimationFrameTrack
                {
                    ClipPath = AssetDatabase.GetAssetPath(clip), SourceClip = clip,
                    Sprites = keys.Select(k => (Sprite)k.value).ToArray(),
                    SpriteTimes = keys.Select(k => k.time).ToArray(),
                    Length = clip.length, IsLooping = clip.isLooping,
                });
                count++;
            }
            Assert.That(count, Is.EqualTo(52));
            // Export only inside the isolated harness. Never rewrite the user's full frame library.
            if (Application.dataPath.Replace('\\', '/').Contains("/output/Diagnostics/house-frame-validation/"))
            {
                const string asset = "Assets/Res/ElementalValidationFrames.asset";
                var existing = AssetDatabase.LoadAssetAtPath<UnitAnimationFrameLibrary>(asset);
                if (existing == null)
                    AssetDatabase.CreateAsset(library, asset);
                else
                {
                    existing.Tracks = library.Tracks;
                    EditorUtility.SetDirty(existing);
                }
                AssetDatabase.SaveAssets();
            }
        }
        finally
        {
            if (!AssetDatabase.Contains(library)) Object.DestroyImmediate(library);
        }
    }
}
