using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.OpenField;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MountainFootRuleTileTests
{
    private const string TileRoot = "Assets/Res/Tile/Mountain/";
    private static readonly Vector3Int[] Directions =
    {
        new(0, 1, 0), new(1, 1, 0), new(1, 0, 0), new(1, -1, 0),
        new(0, -1, 0), new(-1, -1, 0), new(-1, 0, 0), new(-1, 1, 0),
    };

    [Test]
    public void EveryTernaryNeighbourCaseResolvesToAnImportedSprite()
    {
        foreach (string part in new[] { "Foot", "Wall", "Top" })
        {
            MountainRuleTile tile = Load("Mountain" + part + "_Grass1");
            Assert.That(tile.m_DefaultSprite, Is.Not.Null);
            Assert.That(tile.VariantLookup.Length, Is.EqualTo(MountainRuleTile.NeighbourCaseCount));
            Assert.That(tile.neighborPositions.Count, Is.EqualTo(8), "Other RuleTiles must refresh this tile too.");
            Assert.That(tile.VariantSprites.Length, Is.GreaterThan(47));
            foreach (Sprite sprite in tile.VariantSprites)
            {
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(16, 16)));
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16));
            }
            for (int key = 0; key < MountainRuleTile.NeighbourCaseCount; key++)
            {
                int index = tile.VariantLookup[key];
                Assert.That(index, Is.InRange(0, tile.VariantSprites.Length - 1), part + " key=" + key);
                Assert.That(tile.GetSpriteForNeighbourKey(key), Is.SameAs(tile.VariantSprites[index]));
            }
        }
    }

    [Test]
    public void SummitDistinguishesOutsideCliffsAndInsetCorners()
    {
        MountainRuleTile summit = Load("MountainTop_Grass1");
        Sprite fill = summit.GetSpriteForNeighbourKey(6560);
        Sprite rear = summit.GetSpriteForNeighbourKey(6560 - 2);
        Sprite westOutside = summit.GetSpriteForNeighbourKey(6560 - 2 * 729);
        Sprite westCliff = summit.GetSpriteForNeighbourKey(6560 - 729);
        Sprite innerCorner = summit.GetSpriteForNeighbourKey(6560 - 2 * 2187);
        Assert.That(fill, Is.SameAs(summit.m_DefaultSprite));
        Assert.That(rear, Is.Not.SameAs(fill));
        Assert.That(westOutside, Is.Not.SameAs(westCliff));
        Assert.That(westCliff, Is.Not.SameAs(fill));
        Assert.That(innerCorner, Is.Not.SameAs(fill));
    }

    [Test]
    public void AllMountainPartsConnectBidirectionallyButNotToGround()
    {
        MountainRuleTile[] tiles = (from part in new[] { "Foot", "Wall", "Top" }
                                    select Load("Mountain" + part + "_Grass1")).ToArray();
        RuleTile ground = AssetDatabase.LoadAssetAtPath<RuleTile>("Assets/Res/Tile/Grass1.asset");
        Assert.That(ground, Is.Not.Null);
        foreach (MountainRuleTile tile in tiles)
        {
            foreach (MountainRuleTile other in tiles)
            {
                Assert.That(tile.RuleMatch(1, other), Is.True);
                Assert.That(tile.RuleMatch(2, other), Is.False);
                Assert.That(tile.GetNeighbourState(other), Is.EqualTo(other.Part == MountainRuleTile.MountainPart.Summit ? 2 : 1));
            }
            Assert.That(tile.RuleMatch(1, ground), Is.False);
            Assert.That(tile.RuleMatch(2, ground), Is.True);
            Assert.That(tile.RuleMatch(1, null), Is.False);
            Assert.That(tile.RuleMatch(2, null), Is.True);
            Assert.That(tile.GetNeighbourState(ground), Is.Zero);
            Assert.That(tile.GetNeighbourState(null), Is.Zero);
        }
    }

    [Test]
    public void NativeTilemapRefreshUsesTheCompleteTernaryContext()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject root = null;
        try
        {
            root = new GameObject("MountainRuleTileValidation", typeof(Grid)) { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var child = new GameObject("Tiles", typeof(Tilemap)) { hideFlags = HideFlags.HideAndDontSave };
            child.transform.SetParent(root.transform, false);
            Tilemap map = child.GetComponent<Tilemap>();
            var foot = Load("MountainFoot_Grass1");
            var wall = Load("MountainWall_Grass1");
            var summit = Load("MountainTop_Grass1");
            foreach (var centre in new[] { foot, wall, summit })
            {
                map.SetTile(Vector3Int.zero, centre);
                foreach (int key in new[] { 0, 3280, 6560, 6558, 5831, 5102, 2916, 4374, 728, 729, 81, 3279 })
                {
                    int value = key;
                    for (int d = 0; d < 8; d++)
                    {
                        int state = value % 3; value /= 3;
                        map.SetTile(Directions[d], state == 0 ? null : state == 1 ? wall : summit);
                    }
                    Assert.That(map.GetSprite(Vector3Int.zero), Is.SameAs(centre.GetSpriteForNeighbourKey(key)), "key=" + key);
                }
            }
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [MenuItem("Tools/Crystal Magic/Validate Mountain RuleTiles")]
    public static void ValidateConfiguredMountainRuleTiles()
    {
        var tests = new MountainFootRuleTileTests();
        tests.EveryTernaryNeighbourCaseResolvesToAnImportedSprite();
        tests.SummitDistinguishesOutsideCliffsAndInsetCorners();
        tests.AllMountainPartsConnectBidirectionallyButNotToGround();
        tests.NativeTilemapRefreshUsesTheCompleteTernaryContext();
        tests.TransitionColumnsUseTheirFrontGroundStyleAndFallbackWhenUnset();
        tests.ExposedMountainColumnsRemainCliffAtEveryHeight();
        tests.ConnectedPlateausKeepFrontWallsAndPutSummitsBehindThem();
        tests.RearLowSummitsCannotOverwriteFrontCliffsAndRearFeetStayHidden();
        tests.GroundStyleExtensionDoesNotCrossVoidOrChangeSourceAssignments();
        tests.SteppedMountainFrontDoesNotHideCharactersBelowItsLocalFoot();
        tests.MountainColumnGapsStartNewFrontAndFlatFrontsStayBatched();
        System.IO.Directory.CreateDirectory("Temp/MountainFootValidation");
        System.IO.File.WriteAllText("Temp/MountainFootValidation/editor-validation.json",
            "{\"passed\":true,\"nativeTests\":4,\"layoutTests\":7,\"utc\":\"" + DateTime.UtcNow.ToString("O") + "\"}");
        Debug.Log("[MountainRuleTile] PASS: imported sprite references, ternary selection, native Tilemap refresh, layout and local-foot sorting.");
    }

    [Test]
    public void TransitionColumnsUseTheirFrontGroundStyleAndFallbackWhenUnset()
    {
        OpenFieldDungeonLayout layout = MakeLayout(7, 5);
        int[] styles = Enumerable.Repeat(-1, layout.CellCount).ToArray();
        for (int x = 0; x < 7; x++)
        {
            SetTerrain(layout, x, 0, OpenFieldTerrainCell.Ground, 0);
            styles[layout.GetIndex(x, 0)] = x < 3 ? 0 : 1;
        }
        SetTerrain(layout, 1, 1, OpenFieldTerrainCell.Obstacle, 3);
        SetTerrain(layout, 4, 1, OpenFieldTerrainCell.Obstacle, 3);
        OpenFieldDungeonVisualData visual = new();
        visual.GroundStyles = new List<OpenFieldGroundStyleData>
        {
            new() { MountainTransitionRuleTile = Ref("FootA"), MountainWallRuleTile = Ref("WallA"), MountainTopRuleTile = Ref("TopA") },
            new() { MountainTransitionRuleTile = Ref("FootB"), MountainWallRuleTile = Ref("WallB"), MountainTopRuleTile = Ref("TopB") },
        };
        visual.ObstacleVisual.TransitionRuleTile = Ref("Fallback");
        visual.ObstacleVisual.TopRuleTile = Ref("Top");
        visual.ObstacleVisual.WallRuleTile = Ref("Wall");
        List<OpenFieldRuleTilePlacement> placements = Place(layout, visual, styles);
        foreach (string suffix in new[] { "A", "B" })
        {
            Assert.That(placements.Count(p => p.Layer == OpenFieldRuleTileLayer.Obstacle && p.RuleTile.AssetPath == "Foot" + suffix), Is.EqualTo(1));
            Assert.That(placements.Count(p => p.Layer == OpenFieldRuleTileLayer.Obstacle && p.RuleTile.AssetPath == "Wall" + suffix), Is.EqualTo(3));
            Assert.That(placements.Count(p => p.Layer == OpenFieldRuleTileLayer.Obstacle && p.RuleTile.AssetPath == "Top" + suffix), Is.EqualTo(0));
        }
        visual.GroundStyles[1].MountainTransitionRuleTile.AssetPath = string.Empty;
        visual.GroundStyles[1].MountainWallRuleTile.AssetPath = string.Empty;
        visual.GroundStyles[1].MountainTopRuleTile.AssetPath = string.Empty;
        placements = Place(layout, visual, styles);
        Assert.That(placements.Count(p => p.Layer == OpenFieldRuleTileLayer.Obstacle && p.RuleTile.AssetPath == "Fallback"), Is.EqualTo(1));
        Assert.That(placements.Count(p => p.Layer == OpenFieldRuleTileLayer.Obstacle && p.RuleTile.AssetPath == "Wall"), Is.EqualTo(3));
        Assert.That(placements.Count(p => p.Layer == OpenFieldRuleTileLayer.Obstacle && p.RuleTile.AssetPath == "Top"), Is.EqualTo(0));
    }

    [Test]
    public void ExposedMountainColumnsRemainCliffAtEveryHeight()
    {
        for (int height = 1; height <= 4; height++)
        {
            OpenFieldDungeonLayout layout = MakeLayout(3, 7);
            int[] styles = Enumerable.Repeat(-1, layout.CellCount).ToArray();
            SetTerrain(layout, 1, 0, OpenFieldTerrainCell.Ground, 0);
            styles[layout.GetIndex(1, 0)] = 0;
            SetTerrain(layout, 1, 1, OpenFieldTerrainCell.Obstacle, height);
            OpenFieldDungeonVisualData visual = new();
            visual.GroundStyles = new List<OpenFieldGroundStyleData> { new() };
            visual.ObstacleVisual.TransitionRuleTile = Ref("Foot");
            visual.ObstacleVisual.WallRuleTile = Ref("Wall");
            visual.ObstacleVisual.TopRuleTile = Ref("Top");
            var mountain = Place(layout, visual, styles).Where(p => p.Layer == OpenFieldRuleTileLayer.Obstacle).ToArray();
            Assert.That(mountain.Length, Is.EqualTo(height + 1));
            for (int step = 0; step <= height; step++)
            {
                var atCell = mountain.Single(p => p.Cell == new Vector2Int(1, 1 + step));
                Assert.That(atCell.RuleTile.AssetPath, Is.EqualTo(step == 0 ? "Foot" : "Wall"));
            }
        }
    }

    [Test]
    public void ConnectedPlateausKeepFrontWallsAndPutSummitsBehindThem()
    {
        foreach (int height in new[] { 1, 2, 3, 4 })
        {
            OpenFieldDungeonLayout layout = MakeLayout(8, 10);
            FillGround(layout);
            for (int y = 1; y <= 4; y++)
            for (int x = 1; x <= 5; x++)
                SetTerrain(layout, x, y, OpenFieldTerrainCell.Obstacle, height);
            var cells = FinalMountainCells(layout);
            for (int x = 1; x <= 5; x++)
            {
                Assert.That(cells[new Vector2Int(x, 1)], Is.EqualTo("Foot"));
                for (int step = 1; step <= height; step++)
                    Assert.That(cells[new Vector2Int(x, 1 + step)], Is.EqualTo("Wall"), "Front at height=" + height);
                for (int row = 2; row <= 4; row++)
                    Assert.That(cells[new Vector2Int(x, row + height)], Is.EqualTo("Top"));
            }
        }
    }

    [Test]
    public void RearLowSummitsCannotOverwriteFrontCliffsAndRearFeetStayHidden()
    {
        OpenFieldDungeonLayout layout = MakeLayout(4, 9);
        FillGround(layout);
        SetTerrain(layout, 1, 1, OpenFieldTerrainCell.Obstacle, 4);
        SetTerrain(layout, 1, 2, OpenFieldTerrainCell.Obstacle, 1);
        SetTerrain(layout, 1, 3, OpenFieldTerrainCell.Obstacle, 1);
        var cells = FinalMountainCells(layout);
        Assert.That(cells[new Vector2Int(1, 1)], Is.EqualTo("Foot"));
        for (int y = 2; y <= 5; y++) Assert.That(cells[new Vector2Int(1, y)], Is.EqualTo("Wall"));

        SetTerrain(layout, 1, 1, OpenFieldTerrainCell.Obstacle, 1);
        SetTerrain(layout, 1, 2, OpenFieldTerrainCell.Obstacle, 4);
        cells = FinalMountainCells(layout);
        Assert.That(cells[new Vector2Int(1, 2)], Is.EqualTo("Wall"), "Rear foot must not overwrite the front crest");
        for (int y = 3; y <= 6; y++) Assert.That(cells[new Vector2Int(1, y)], Is.EqualTo("Wall"));
    }

    // The offline renderer consumes exactly the same final placement map as runtime,
    // not a hand-arranged atlas demonstration. No Unity scene state is changed.
    public string ExportConnectedMountainPreview()
    {
        OpenFieldDungeonLayout layout = MakeLayout(44, 18);
        FillGround(layout);
        for (int y = 2; y <= 7; y++)
        for (int x = 1; x <= 10; x++) SetTerrain(layout, x, y, OpenFieldTerrainCell.Obstacle, 1);
        for (int y = 2; y <= 7; y++)
        for (int x = 14; x <= 23; x++) SetTerrain(layout, x, y, OpenFieldTerrainCell.Obstacle, 3);
        for (int y = 2; y <= 10; y++)
        for (int x = 28; x <= 41; x++)
        {
            if (x >= 36 && y < 5) continue;
            int height = y < 5 ? 4 : y < 8 ? 1 : 3;
            SetTerrain(layout, x, y, OpenFieldTerrainCell.Obstacle, height);
        }
        var rows = FinalMountainCells(layout).Select(p => "{\"x\":" + p.Key.x + ",\"y\":" + p.Key.y + ",\"part\":" + (p.Value == "Foot" ? 0 : p.Value == "Wall" ? 1 : 2) + "}");
        return "{\"width\":44,\"height\":18,\"cells\":[" + string.Join(",", rows) + "]}";
    }

    private static void FillGround(OpenFieldDungeonLayout layout)
    {
        for (int y = 0; y < layout.Height; y++)
        for (int x = 0; x < layout.Width; x++) SetTerrain(layout, x, y, OpenFieldTerrainCell.Ground, 0);
    }

    private static Dictionary<Vector2Int, string> FinalMountainCells(OpenFieldDungeonLayout layout)
    {
        var visual = new OpenFieldDungeonVisualData { GroundStyles = new List<OpenFieldGroundStyleData> { new() } };
        visual.ObstacleVisual.TransitionRuleTile = Ref("Foot");
        visual.ObstacleVisual.WallRuleTile = Ref("Wall");
        visual.ObstacleVisual.TopRuleTile = Ref("Top");
        return Place(layout, visual, new int[layout.CellCount]).Where(p => p.Layer == OpenFieldRuleTileLayer.Obstacle)
            .GroupBy(p => p.Cell).ToDictionary(g => g.Key, g => g.Last().RuleTile.AssetPath);
    }

    [Test]
    public void GroundStyleExtensionDoesNotCrossVoidOrChangeSourceAssignments()
    {
        OpenFieldDungeonLayout layout = MakeLayout(4, 3);
        int[] styles = Enumerable.Repeat(-1, layout.CellCount).ToArray();
        SetTerrain(layout, 0, 0, OpenFieldTerrainCell.Ground, 0);
        styles[layout.GetIndex(0, 0)] = 2;
        SetTerrain(layout, 0, 1, OpenFieldTerrainCell.Obstacle, 1);
        SetTerrain(layout, 0, 2, OpenFieldTerrainCell.Obstacle, 1);
        SetTerrain(layout, 3, 0, OpenFieldTerrainCell.Obstacle, 1);
        MethodInfo method = typeof(OpenFieldDungeonVisualLayoutBuilder).GetMethod("ExtendGroundStylesIntoMountains", BindingFlags.NonPublic | BindingFlags.Static);
        int[] extended = (int[])method.Invoke(null, new object[] { layout, styles });
        Assert.That(extended[layout.GetIndex(0, 2)], Is.EqualTo(2));
        Assert.That(extended[layout.GetIndex(3, 0)], Is.EqualTo(-1));
        Assert.That(styles[layout.GetIndex(0, 1)], Is.EqualTo(-1));
    }

    private static MountainRuleTile Load(string name)
    {
        MountainRuleTile tile = AssetDatabase.LoadAssetAtPath<MountainRuleTile>(TileRoot + name + ".asset");
        Assert.That(tile, Is.Not.Null, name);
        return tile;
    }

    [Test]
    public void SteppedMountainFrontDoesNotHideCharactersBelowItsLocalFoot()
    {
        var placements = new List<RuntimeDungeonRuleTilePlacement>();
        AddColumn(placements, 0, 0, 6);
        AddColumn(placements, 1, 4, 6);
        AddColumn(placements, 2, 4, 6);
        object[] groups = RenderGroups(placements);
        Assert.That(groups.Length, Is.EqualTo(2));
        object raisedFront = groups.Single(g => GroupTiles(g).Any(p => p.Cell == new Vector2Int(1, 4)));
        Assert.That(FootY(raisedFront), Is.EqualTo(4));
        Assert.That(GroupTiles(raisedFront).Select(p => p.Cell.y).Min(), Is.EqualTo(4));
        foreach (float origin in new[] { -20f, 0f, 20f })
        foreach (float scale in new[] { 0.5f, 1f, 2f })
        {
            float footWorldY = origin + FootY(raisedFront) * scale;
            int mountainOrder = MountainOrder(footWorldY);
            Assert.That(Mathf.RoundToInt(-(footWorldY - 0.5f * scale) * 100f), Is.GreaterThan(mountainOrder), "In front must be visible");
            Assert.That(Mathf.RoundToInt(-footWorldY * 100f), Is.GreaterThan(mountainOrder), "At foot line must be visible");
            Assert.That(Mathf.RoundToInt(-(footWorldY + 0.5f * scale) * 100f), Is.LessThan(mountainOrder), "Behind must still be occluded");
        }
    }

    [Test]
    public void MountainColumnGapsStartNewFrontAndFlatFrontsStayBatched()
    {
        var placements = new List<RuntimeDungeonRuleTilePlacement>();
        AddColumn(placements, 0, 0, 6);
        AddColumn(placements, 1, 0, 1);
        AddColumn(placements, 1, 4, 6);
        object[] groups = RenderGroups(placements);
        Assert.That(groups.Length, Is.EqualTo(2));
        Assert.That(FootY(groups.Single(g => GroupTiles(g).Any(p => p.Cell == new Vector2Int(1, 4)))), Is.EqualTo(4));
        Assert.That(FootY(groups.Single(g => GroupTiles(g).Any(p => p.Cell == new Vector2Int(1, 1)))), Is.EqualTo(0));
        placements.Clear();
        for (int x = 0; x < 20; x++) AddColumn(placements, x, 2, 7);
        var replacement = new RuntimeDungeonRuleTilePlacement { Layer = RuntimeDungeonTilemapLayer.Obstacle, Cell = new Vector2Int(5, 4), RuleTilePath = "LastWins" };
        placements.Add(replacement);
        groups = RenderGroups(placements);
        Assert.That(groups.Length, Is.EqualTo(1), "Flat fronts should not create one renderer per column");
        Assert.That(GroupTiles(groups[0]).Count, Is.EqualTo(120));
        Assert.That(GroupTiles(groups[0]).Single(p => p.Cell == replacement.Cell), Is.SameAs(replacement));
    }

    private static Type RenderBuilder => typeof(MountainRuleTile).Assembly.GetType("CrystalMagic.Core.DungeonRuleTileVisualBuilder", true);
    private static object[] RenderGroups(List<RuntimeDungeonRuleTilePlacement> placements) =>
        ((System.Collections.IEnumerable)RenderBuilder.GetMethod("GetObstacleRenderGroups", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { placements })).Cast<object>().ToArray();
    private static int MountainOrder(float y) => (int)RenderBuilder.GetMethod("GetObstacleSortingOrder", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { y });
    private static int FootY(object group) => (int)group.GetType().GetProperty("LowestCellY").GetValue(group);
    private static List<RuntimeDungeonRuleTilePlacement> GroupTiles(object group) => (List<RuntimeDungeonRuleTilePlacement>)group.GetType().GetProperty("Placements").GetValue(group);
    private static void AddColumn(List<RuntimeDungeonRuleTilePlacement> placements, int x, int bottom, int top)
    {
        for (int y = bottom; y <= top; y++)
            placements.Add(new RuntimeDungeonRuleTilePlacement { Layer = RuntimeDungeonTilemapLayer.Obstacle, RuleTilePath = "Mountain", Cell = new Vector2Int(x, y) });
    }

    private static OpenFieldRuleTileReferenceData Ref(string path) => new() { AssetPath = path };
    private static OpenFieldDungeonLayout MakeLayout(int width, int height) =>
        (OpenFieldDungeonLayout)Activator.CreateInstance(typeof(OpenFieldDungeonLayout), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { width, height, 123 }, null);
    private static void SetTerrain(OpenFieldDungeonLayout layout, int x, int y, OpenFieldTerrainCell cell, int height) =>
        typeof(OpenFieldDungeonLayout).GetMethod("SetTerrain", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(layout, new object[] { x, y, 0.5f, cell, height });
    private static List<OpenFieldRuleTilePlacement> Place(OpenFieldDungeonLayout layout, OpenFieldDungeonVisualData visual, int[] styles) =>
        (List<OpenFieldRuleTilePlacement>)typeof(OpenFieldDungeonVisualLayoutBuilder).GetMethod("CreateTerrainPlacements", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { layout, visual, styles });
}
