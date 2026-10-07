using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SkillAdditionIconTests
{
    private const string Root = "Assets/Res/Sprites/SkillAddition/";

    [Test]
    public void EveryAdditionHasItsOwnLoadable64PixelSprite()
    {
        var rows = (JArray)JObject.Parse(File.ReadAllText("Assets/Res/Data/SkillAdditionDataTable.json"))["Rows"];
        var paths = new HashSet<string>();
        foreach (JObject row in rows.Cast<JObject>())
        {
            string path = (string)row["IconPath"];
            string name = (string)row["NameKey"];
            Assert.That(path, Does.StartWith(Root), name);
            Assert.That(paths.Add(path), Is.True, name + " must have a distinct icon.");
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(icon, Is.Not.Null, name);
            Assert.That(icon.texture.width, Is.EqualTo(64), name);
            Assert.That(icon.texture.height, Is.EqualTo(64), name);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), name);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), name);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), name);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), name);
            Assert.That(importer.mipmapEnabled, Is.False, name);
            Assert.That(importer.alphaIsTransparency, Is.True, name);
        }
        Assert.That(rows.Count, Is.EqualTo(10));
    }
}
