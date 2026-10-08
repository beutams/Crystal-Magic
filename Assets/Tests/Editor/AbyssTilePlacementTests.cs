using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.OpenField;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class AbyssTilePlacementTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void GroundAboveVoidBuildsTransitionWallAndBlackDownward()
    {
        OpenFieldDungeonLayout layout = (OpenFieldDungeonLayout)Activator.CreateInstance(
            typeof(OpenFieldDungeonLayout), PrivateInstance, null, new object[] { 5, 6, 123 }, null);
        MethodInfo setTerrain = typeof(OpenFieldDungeonLayout).GetMethod("SetTerrain", PrivateInstance);
        for (int y = 0; y < layout.Height; y++)
        for (int x = 0; x < layout.Width; x++)
            setTerrain.Invoke(layout, new object[] { x, y, 0.1f, OpenFieldTerrainCell.Void, 0 });
        for (int x = 0; x < layout.Width; x++)
            setTerrain.Invoke(layout, new object[] { x, 5, 0.5f, OpenFieldTerrainCell.Ground, 0 });

        OpenFieldDungeonVisualData visual = new()
        {
            GroundStyles = new List<OpenFieldGroundStyleData>
            {
                new() { VoidTransitionRuleTile = Ref("StyleEdge") },
            },
            VoidVisual = new OpenFieldVoidVisualData
            {
                TransitionRuleTile = Ref("Transition"),
                WallRuleTile = Ref("Wall"),
                WallBottomRuleTile = Ref("WallBottom"),
                AbyssRuleTile = Ref("Black"),
            },
        };
        MethodInfo createPlacements = typeof(OpenFieldDungeonVisualLayoutBuilder).GetMethod(
            "CreateTerrainPlacements", BindingFlags.Static | BindingFlags.NonPublic);
        List<OpenFieldRuleTilePlacement> placements = (List<OpenFieldRuleTilePlacement>)createPlacements.Invoke(
            null, new object[] { layout, visual, new int[layout.CellCount] });

        string[] expected = { "Black", "Black", "WallBottom", "Wall", "StyleEdge" };
        for (int y = 0; y < expected.Length; y++)
        {
            OpenFieldRuleTilePlacement tile = placements.Single(p =>
                p.Layer == OpenFieldRuleTileLayer.Void && p.Cell == new Vector2Int(2, y));
            Assert.That(tile.RuleTile.AssetPath, Is.EqualTo(expected[y]), $"Void cell y={y}");
        }
        Assert.That(placements.Any(p => p.Layer == OpenFieldRuleTileLayer.Void &&
            p.Cell == new Vector2Int(2, 5)), Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void GrassBesideVoidBuildsSideTransitionWallFadeAndBlack(bool voidOnLeft)
    {
        OpenFieldDungeonLayout layout = (OpenFieldDungeonLayout)Activator.CreateInstance(
            typeof(OpenFieldDungeonLayout), PrivateInstance, null, new object[] { 6, 3, 123 }, null);
        MethodInfo setTerrain = typeof(OpenFieldDungeonLayout).GetMethod("SetTerrain", PrivateInstance);
        for (int y = 0; y < layout.Height; y++)
        for (int x = 0; x < layout.Width; x++)
            setTerrain.Invoke(layout, new object[] { x, y, 0.1f, OpenFieldTerrainCell.Void, 0 });
        int groundX = voidOnLeft ? 5 : 0;
        setTerrain.Invoke(layout, new object[] { groundX, 1, 0.5f, OpenFieldTerrainCell.Ground, 0 });

        OpenFieldDungeonVisualData visual = new()
        {
            GroundStyles = new List<OpenFieldGroundStyleData>
            {
                new()
                {
                    VoidLeftTransitionRuleTile = Ref("StyleLeft"),
                    VoidRightTransitionRuleTile = Ref("StyleRight"),
                },
            },
            VoidVisual = new OpenFieldVoidVisualData
            {
                AbyssRuleTile = Ref("Black"),
                WallRuleTile = Ref("Wall"),
                LeftFadeRuleTile = Ref("LeftFade"),
                RightFadeRuleTile = Ref("RightFade"),
            },
        };
        MethodInfo createPlacements = typeof(OpenFieldDungeonVisualLayoutBuilder).GetMethod(
            "CreateTerrainPlacements", BindingFlags.Static | BindingFlags.NonPublic);
        List<OpenFieldRuleTilePlacement> placements = (List<OpenFieldRuleTilePlacement>)createPlacements.Invoke(
            null, new object[] { layout, visual, new int[layout.CellCount] });

        string[] expected = voidOnLeft
            ? new[] { "StyleLeft", "Wall", "LeftFade", "Black", "Black" }
            : new[] { "StyleRight", "Wall", "RightFade", "Black", "Black" };
        for (int distance = 1; distance <= 5; distance++)
        {
            int x = voidOnLeft ? groundX - distance : groundX + distance;
            OpenFieldRuleTilePlacement tile = placements.Single(p =>
                p.Layer == OpenFieldRuleTileLayer.Void && p.Cell == new Vector2Int(x, 1));
            Assert.That(tile.RuleTile.AssetPath, Is.EqualTo(expected[distance - 1]));
        }
    }

    [Test]
    public void DiagonalVoidCellsJoinTheFrontAndSideStrips()
    {
        OpenFieldDungeonLayout layout = (OpenFieldDungeonLayout)Activator.CreateInstance(
            typeof(OpenFieldDungeonLayout), PrivateInstance, null, new object[] { 3, 3, 123 }, null);
        MethodInfo setTerrain = typeof(OpenFieldDungeonLayout).GetMethod("SetTerrain", PrivateInstance);
        for (int y = 0; y < layout.Height; y++)
        for (int x = 0; x < layout.Width; x++)
            setTerrain.Invoke(layout, new object[] { x, y, 0.1f, OpenFieldTerrainCell.Void, 0 });
        setTerrain.Invoke(layout, new object[] { 1, 1, 0.5f, OpenFieldTerrainCell.Ground, 0 });

        OpenFieldDungeonVisualData visual = new()
        {
            GroundStyles = new List<OpenFieldGroundStyleData>
            {
                new()
                {
                    VoidLeftCornerRuleTile = Ref("LeftCorner"),
                    VoidRightCornerRuleTile = Ref("RightCorner"),
                },
            },
            VoidVisual = new OpenFieldVoidVisualData { AbyssRuleTile = Ref("Black") },
        };
        MethodInfo createPlacements = typeof(OpenFieldDungeonVisualLayoutBuilder).GetMethod(
            "CreateTerrainPlacements", BindingFlags.Static | BindingFlags.NonPublic);
        List<OpenFieldRuleTilePlacement> placements = (List<OpenFieldRuleTilePlacement>)createPlacements.Invoke(
            null, new object[] { layout, visual, new int[layout.CellCount] });
        Assert.That(placements.Single(p => p.Layer == OpenFieldRuleTileLayer.Void &&
            p.Cell == new Vector2Int(0, 0)).RuleTile.AssetPath, Is.EqualTo("LeftCorner"));
        Assert.That(placements.Single(p => p.Layer == OpenFieldRuleTileLayer.Void &&
            p.Cell == new Vector2Int(2, 0)).RuleTile.AssetPath, Is.EqualTo("RightCorner"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void VoidTouchingGrassAboveAndBesideUsesInnerCorner(bool voidOnLeft)
    {
        OpenFieldDungeonLayout layout = (OpenFieldDungeonLayout)Activator.CreateInstance(
            typeof(OpenFieldDungeonLayout), PrivateInstance, null, new object[] { 3, 3, 123 }, null);
        MethodInfo setTerrain = typeof(OpenFieldDungeonLayout).GetMethod("SetTerrain", PrivateInstance);
        for (int y = 0; y < layout.Height; y++)
        for (int x = 0; x < layout.Width; x++)
            setTerrain.Invoke(layout, new object[] { x, y, 0.1f, OpenFieldTerrainCell.Void, 0 });
        setTerrain.Invoke(layout, new object[] { 1, 2, 0.5f, OpenFieldTerrainCell.Ground, 0 });
        setTerrain.Invoke(layout, new object[] { voidOnLeft ? 2 : 0, 1, 0.5f, OpenFieldTerrainCell.Ground, 0 });

        OpenFieldDungeonVisualData visual = new()
        {
            GroundStyles = new List<OpenFieldGroundStyleData>
            {
                new()
                {
                    VoidLeftInnerCornerRuleTile = Ref("LeftInner"),
                    VoidRightInnerCornerRuleTile = Ref("RightInner"),
                },
            },
            VoidVisual = new OpenFieldVoidVisualData { AbyssRuleTile = Ref("Black") },
        };
        MethodInfo createPlacements = typeof(OpenFieldDungeonVisualLayoutBuilder).GetMethod(
            "CreateTerrainPlacements", BindingFlags.Static | BindingFlags.NonPublic);
        List<OpenFieldRuleTilePlacement> placements = (List<OpenFieldRuleTilePlacement>)createPlacements.Invoke(
            null, new object[] { layout, visual, new int[layout.CellCount] });
        OpenFieldRuleTilePlacement corner = placements.Single(p => p.Layer == OpenFieldRuleTileLayer.Void &&
            p.Cell == new Vector2Int(1, 1));
        Assert.That(corner.RuleTile.AssetPath, Is.EqualTo(voidOnLeft ? "LeftInner" : "RightInner"));
    }

    [Test]
    public void AllAbyssRuleTilesResolveToSixteenPixelSprites()
    {
        foreach (string name in new[]
        {
            "AbyssTransition_Grass1", "AbyssTransition_Grass2", "AbyssTransition_Grass3",
            "AbyssTransition_Grass1_Left", "AbyssTransition_Grass1_Right",
            "AbyssTransition_Grass2_Left", "AbyssTransition_Grass2_Right",
            "AbyssTransition_Grass3_Left", "AbyssTransition_Grass3_Right",
            "AbyssCorner_Grass1_Left", "AbyssCorner_Grass1_Right",
            "AbyssCorner_Grass2_Left", "AbyssCorner_Grass2_Right",
            "AbyssCorner_Grass3_Left", "AbyssCorner_Grass3_Right",
            "AbyssInnerCorner_Grass1_Left", "AbyssInnerCorner_Grass1_Right",
            "AbyssInnerCorner_Grass2_Left", "AbyssInnerCorner_Grass2_Right",
            "AbyssInnerCorner_Grass3_Left", "AbyssInnerCorner_Grass3_Right",
            "AbyssWall", "AbyssWallBottom", "AbyssFade_Left", "AbyssFade_Right", "AbyssBlack",
        })
        {
            string root = "Assets/Res/";
            RuleTile tile = AssetDatabase.LoadAssetAtPath<RuleTile>($"{root}Tile/Abyss/{name}.asset");
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{root}Sprites/Dungeon/Abyss/{name}_16.png");
            Assert.That(tile, Is.Not.Null, name);
            Assert.That(sprite, Is.Not.Null, name);
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(16, 16)), name);
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16f), name);
            Assert.That(new SerializedObject(tile).FindProperty("m_DefaultSprite").objectReferenceValue,
                Is.SameAs(sprite), name);
        }
    }

    [Test]
    public void PrairieThemeUsesInstalledAbyssRuleTiles()
    {
        string json = File.ReadAllText(Path.Combine(Application.dataPath,
            "Res/Data/DungeonThemeDataTable.json"));
        DungeonThemeData prairie = JsonConvert.DeserializeObject<ThemeTable>(json).Rows
            .Single(theme => theme.ThemeKey == "Prairie");
        OpenFieldDungeonVisualData visual = prairie.OpenField.Visual;
        const string root = "Assets/Res/Tile/Abyss/";

        Assert.That(visual.VoidVisual.TransitionRuleTile.AssetPath,
            Is.EqualTo(root + "AbyssTransition_Grass1.asset"));
        Assert.That(visual.VoidVisual.WallRuleTile.AssetPath,
            Is.EqualTo(root + "AbyssWall.asset"));
        Assert.That(visual.VoidVisual.WallBottomRuleTile.AssetPath,
            Is.EqualTo(root + "AbyssWallBottom.asset"));
        Assert.That(visual.VoidVisual.AbyssRuleTile.AssetPath,
            Is.EqualTo(root + "AbyssBlack.asset"));
        Assert.That(visual.VoidVisual.LeftTransitionRuleTile.AssetPath,
            Is.EqualTo(root + "AbyssTransition_Grass1_Left.asset"));
        Assert.That(visual.VoidVisual.RightTransitionRuleTile.AssetPath,
            Is.EqualTo(root + "AbyssTransition_Grass1_Right.asset"));
        Assert.That(visual.VoidVisual.LeftFadeRuleTile.AssetPath,
            Is.EqualTo(root + "AbyssFade_Left.asset"));
        Assert.That(visual.VoidVisual.RightFadeRuleTile.AssetPath,
            Is.EqualTo(root + "AbyssFade_Right.asset"));
        Assert.That(visual.VoidVisual.LeftCornerRuleTile.AssetPath,
            Is.EqualTo(root + "AbyssCorner_Grass1_Left.asset"));
        Assert.That(visual.VoidVisual.RightCornerRuleTile.AssetPath,
            Is.EqualTo(root + "AbyssCorner_Grass1_Right.asset"));
        Assert.That(visual.VoidVisual.LeftInnerCornerRuleTile.AssetPath,
            Is.EqualTo(root + "AbyssInnerCorner_Grass1_Left.asset"));
        Assert.That(visual.VoidVisual.RightInnerCornerRuleTile.AssetPath,
            Is.EqualTo(root + "AbyssInnerCorner_Grass1_Right.asset"));
        for (int i = 1; i <= 3; i++)
        {
            OpenFieldGroundStyleData style = visual.GroundStyles.Single(s => s.Name == $"Grass{i}");
            Assert.That(style.VoidTransitionRuleTile.AssetPath,
                Is.EqualTo(root + $"AbyssTransition_Grass{i}.asset"));
            Assert.That(style.VoidLeftTransitionRuleTile.AssetPath,
                Is.EqualTo(root + $"AbyssTransition_Grass{i}_Left.asset"));
            Assert.That(style.VoidRightTransitionRuleTile.AssetPath,
                Is.EqualTo(root + $"AbyssTransition_Grass{i}_Right.asset"));
            Assert.That(style.VoidLeftCornerRuleTile.AssetPath,
                Is.EqualTo(root + $"AbyssCorner_Grass{i}_Left.asset"));
            Assert.That(style.VoidRightCornerRuleTile.AssetPath,
                Is.EqualTo(root + $"AbyssCorner_Grass{i}_Right.asset"));
            Assert.That(style.VoidLeftInnerCornerRuleTile.AssetPath,
                Is.EqualTo(root + $"AbyssInnerCorner_Grass{i}_Left.asset"));
            Assert.That(style.VoidRightInnerCornerRuleTile.AssetPath,
                Is.EqualTo(root + $"AbyssInnerCorner_Grass{i}_Right.asset"));
        }
    }

    private static OpenFieldRuleTileReferenceData Ref(string path) => new() { AssetPath = path };

    private sealed class ThemeTable
    {
        public List<DungeonThemeData> Rows = new();
    }
}
