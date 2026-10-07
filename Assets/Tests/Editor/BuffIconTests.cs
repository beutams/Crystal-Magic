using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BuffIconTests
{
    private const string IconRoot = "Assets/Res/Sprites/Buff/";

    [Test]
    public void EveryBuffHasItsOwnLoadable64PixelSprite()
    {
        var rows = (JArray)JObject.Parse(File.ReadAllText("Assets/Res/Data/BuffDataTable.json"))["Rows"];
        Assert.That(rows, Is.Not.Empty);
        var paths = new HashSet<string>();
        var ids = new HashSet<int>();

        foreach (JObject row in rows)
        {
            string context = row["Id"] + ": " + row["NameKey"];
            string path = (string)row["IconPath"];
            Assert.That(ids.Add((int)row["Id"]), Is.True, context + " has a duplicate ID.");
            Assert.That(path, Does.StartWith(IconRoot), context);
            Assert.That(paths.Add(path), Is.True, context + " must have a distinct icon.");
            Assert.That(File.Exists(path), Is.True, context);

            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(icon, Is.Not.Null, context);
            Assert.That(icon.texture.width, Is.EqualTo(64), context);
            Assert.That(icon.texture.height, Is.EqualTo(64), context);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, context);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), context);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), context);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), context);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), context);
            Assert.That(importer.mipmapEnabled, Is.False, context);
            Assert.That(importer.alphaIsTransparency, Is.True, context);
            Assert.That(importer.maxTextureSize, Is.EqualTo(64), context);
        }
    }
}
