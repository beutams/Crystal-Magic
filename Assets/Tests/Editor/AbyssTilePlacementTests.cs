using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.OpenField;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class AbyssTilePlacementTests
{
    private const string TilePath = "Assets/Res/Tile/Abyss/AbyssSingleCell.asset";
    private const string SheetPath = "Assets/Res/Sprites/Dungeon/Abyss/AbyssSingleCell.png";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector3Int[] Directions =
    {
        new(0, 1), new(1, 1), new(1, 0), new(1, -1),
        new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1),
    };

    [TestCase(0, "straight-N")]
    [TestCase(2, "straight-E")]
    [TestCase(4, "straight-S")]
    [TestCase(6, "straight-W")]
    public void OnlyFirstVoidCellContainsTransitionInEveryDirection(int direction, string name)
    {
        OpenFieldDungeonLayout layout = MakeLayout();
        for (int i = 0; i < 7; i++)
        {
            Vector2Int ground = direction switch
            {
                0 => new Vector2Int(i, 6),
                2 => new Vector2Int(6, i),
                4 => new Vector2Int(i, 0),
                _ => new Vector2Int(0, i),
            };
            SetGround(layout, ground);
        }

        WithMap(map =>
        {
            Populate(map, Place(layout));
            Vector3Int edge = new Vector3Int(3, 3) + Directions[direction] * 2;
            Assert.That(map.GetSprite(edge).name, Is.EqualTo("Abyss_" + name));
            for (int distance = 1; distance <= 4; distance++)
                Assert.That(map.GetSprite(edge - Directions[direction] * distance),
                    Is.SameAs(Load().m_DefaultSprite), $"Extra wall/fade at distance {distance}");
        });
    }

    [Test]
    public void AllVoidCellsShareOneTileAndGroundIsNotOverwritten()
    {
        OpenFieldDungeonLayout layout = MakeLayout();
        SetGround(layout, new Vector2Int(3, 3));
        SetGround(layout, new Vector2Int(4, 3));
        var placements = Place(layout);
        var voidTiles = placements.Where(p => p.Layer == OpenFieldRuleTileLayer.Void).ToList();
        Assert.That(voidTiles.All(p => p.RuleTile.AssetPath == TilePath), Is.True);
        Assert.That(voidTiles.GroupBy(p => p.Cell).All(g => g.Count() == 1), Is.True);
        Assert.That(voidTiles.Count(p => layout.IsInside(p.Cell.x, p.Cell.y)), Is.EqualTo(47));
        Assert.That(voidTiles.Any(p => p.Cell == new Vector2Int(3, 3) || p.Cell == new Vector2Int(4, 3)), Is.False);
        Assert.That(placements.Count(p => p.Layer == OpenFieldRuleTileLayer.Ground), Is.EqualTo(2));
    }

    [Test]
    public void OuterBoundaryDoesNotCreateAnImaginaryGrassShore()
    {
        WithMap(map =>
        {
            Populate(map, Place(MakeLayout()));
            for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++)
                Assert.That(map.GetSprite(new Vector3Int(x, y)), Is.SameAs(Load().m_DefaultSprite));
        });
    }

    [Test]
    public void EveryNeighbourMaskResolvesExactlyOnceWithoutRandomnessOrRotation()
    {
        RuleTile tile = Load();
        Assert.That(tile.m_TilingRules.Count, Is.EqualTo(47));
        WithMap(map =>
        {
            for (int mask = 0; mask < 256; mask++)
            {
                map.ClearAllTiles();
                map.SetTile(Vector3Int.zero, tile);
                for (int bit = 0; bit < 8; bit++)
                    if ((mask & (1 << bit)) != 0) map.SetTile(Directions[bit], tile);
                map.RefreshAllTiles();

                var matches = tile.m_TilingRules.Where(rule =>
                    rule.m_NeighborPositions.Select((position, i) =>
                        tile.RuleMatch(rule.m_Neighbors[i], map.GetTile(position))).All(v => v)).ToList();
                Assert.That(matches.Count, Is.EqualTo(1), "Mask " + mask);
                var match = matches.Single();
                Assert.That(match.m_Id, Is.EqualTo(Normalize(mask)));
                Assert.That(match.m_Output, Is.EqualTo(RuleTile.TilingRuleOutput.OutputSprite.Single));
                Assert.That(match.m_Sprites.Length, Is.EqualTo(1));
                Assert.That(map.GetSprite(Vector3Int.zero), Is.SameAs(match.m_Sprites[0]), "Mask " + mask);
                Assert.That(map.GetTransformMatrix(Vector3Int.zero), Is.EqualTo(Matrix4x4.identity));
                Assert.That(map.GetColliderType(Vector3Int.zero), Is.EqualTo(Tile.ColliderType.None));
            }
        });
    }

    [Test]
    public void NeighbourChangesRefreshTheSameRuleTile()
    {
        WithMap(map =>
        {
            RuleTile tile = Load();
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++) map.SetTile(new Vector3Int(x, y), tile);
            Assert.That(map.GetSprite(Vector3Int.zero), Is.SameAs(tile.m_DefaultSprite));
            map.SetTile(Vector3Int.up, null);
            Assert.That(map.GetSprite(Vector3Int.zero).name, Is.EqualTo("Abyss_straight-N"));
            map.SetTile(Vector3Int.up, tile);
            Assert.That(map.GetSprite(Vector3Int.zero), Is.SameAs(tile.m_DefaultSprite));
        });
    }

    [TestCase(124, "straight-N")]
    [TestCase(241, "straight-E")]
    [TestCase(199, "straight-S")]
    [TestCase(31, "straight-W")]
    [TestCase(28, "outer-NW")]
    [TestCase(112, "outer-NE")]
    [TestCase(193, "outer-SE")]
    [TestCase(7, "outer-SW")]
    [TestCase(127, "inner-NW")]
    [TestCase(253, "inner-NE")]
    [TestCase(247, "inner-SE")]
    [TestCase(223, "inner-SW")]
    public void InstalledMasterIsPixelIdenticalToApprovedPreview(int mask, string name)
    {
        Sprite sprite = Load().m_TilingRules.Single(rule => rule.m_Id == mask).m_Sprites.Single();
        Assert.That(sprite.name, Is.EqualTo("Abyss_" + name));
        Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(16, 16)));
        Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16));
        Texture2D sheet = ReadPng(SheetPath);
        Texture2D master = ReadPng("Tools/Art/AbyssSingleCell/Masters/" + name + ".png");
        try
        {
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                Assert.That((Color32)sheet.GetPixel((int)sprite.rect.x + x, (int)sprite.rect.y + y),
                    Is.EqualTo((Color32)master.GetPixel(x, y)), $"{name}: {x},{y}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
            UnityEngine.Object.DestroyImmediate(master);
        }
    }

    [Test]
    public void ApprovedSevenBySevenLayoutResolvesTheSameMasterTiles()
    {
        string[] cells = { "GGGGGGG", "GGPPPPG", "GPPPPPG", "GPPPPPG", "GPPPPGG", "GPPPPGG", "GGGGGGG" };
        int[][] ids =
        {
            new[] {-1,-1,-1,-1,-1,-1,-1},
            new[] {-1,-1,28,124,124,112,-1},
            new[] {-1,28,127,255,255,241,-1},
            new[] {-1,31,255,255,247,193,-1},
            new[] {-1,31,255,255,241,-1,-1},
            new[] {-1,7,199,199,193,-1,-1},
            new[] {-1,-1,-1,-1,-1,-1,-1},
        };
        var layout = MakeLayout();
        for (int row = 0; row < 7; row++)
        for (int x = 0; x < 7; x++)
            if (cells[row][x] == 'G') SetGround(layout, new Vector2Int(x, 6 - row));
        WithMap(map =>
        {
            Populate(map, Place(layout));
            for (int row = 0; row < 7; row++)
            for (int x = 0; x < 7; x++)
            {
                if (ids[row][x] < 0) continue;
                Sprite expected = Load().m_TilingRules.Single(rule => rule.m_Id == ids[row][x]).m_Sprites.Single();
                Assert.That(map.GetSprite(new Vector3Int(x, 6 - row)), Is.SameAs(expected), $"{x},{row}");
            }
        });
    }

    [Test]
    public void PrairieUsesOneAbyssAssetAndTheImporterPreservesPixelArt()
    {
        JObject table = JObject.Parse(File.ReadAllText("Assets/Res/Data/DungeonThemeDataTable.json"));
        var visual = table["Rows"].Single(t => (string)t["ThemeKey"] == "Prairie")["OpenField"]["Visual"];
        Assert.That((string)visual["VoidVisual"]["AbyssRuleTile"]["AssetPath"], Is.EqualTo(TilePath));
        Assert.That(((JObject)visual["VoidVisual"]).Properties().Count(), Is.EqualTo(1));
        foreach (JObject style in visual["GroundStyles"])
            Assert.That(style.Properties().Any(p => p.Name.StartsWith("Void", StringComparison.Ordinal)), Is.False);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16));
        Assert.That(Load().m_DefaultSprite.name, Is.EqualTo("Abyss_black"));
    }

    private static int Normalize(int mask)
    {
        foreach (int bit in new[] {1, 3, 5, 7})
            if ((mask & (1 << (bit - 1))) == 0 || (mask & (1 << ((bit + 1) % 8))) == 0)
                mask &= ~(1 << bit);
        return mask;
    }

    private static RuleTile Load()
    {
        RuleTile tile = AssetDatabase.LoadAssetAtPath<RuleTile>(TilePath);
        Assert.That(tile, Is.Not.Null);
        return tile;
    }

    private static Texture2D ReadPng(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
        return texture;
    }

    private static OpenFieldDungeonLayout MakeLayout()
    {
        return (OpenFieldDungeonLayout)Activator.CreateInstance(
            typeof(OpenFieldDungeonLayout), PrivateInstance, null, new object[] {7, 7, 123}, null);
    }

    private static void SetGround(OpenFieldDungeonLayout layout, Vector2Int cell)
    {
        typeof(OpenFieldDungeonLayout).GetMethod("SetTerrain", PrivateInstance).Invoke(
            layout, new object[] {cell.x, cell.y, 0.5f, OpenFieldTerrainCell.Ground, 0});
    }

    private static List<OpenFieldRuleTilePlacement> Place(OpenFieldDungeonLayout layout)
    {
        var visual = new OpenFieldDungeonVisualData
        {
            GroundStyles = new List<OpenFieldGroundStyleData> {new()},
            VoidVisual = new OpenFieldVoidVisualData {AbyssRuleTile = new() {AssetPath = TilePath}},
        };
        return (List<OpenFieldRuleTilePlacement>)typeof(OpenFieldDungeonVisualLayoutBuilder)
            .GetMethod("CreateTerrainPlacements", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] {layout, visual, new int[layout.CellCount]});
    }

    private static void Populate(Tilemap map, IEnumerable<OpenFieldRuleTilePlacement> placements)
    {
        foreach (var placement in placements.Where(p => p.Layer == OpenFieldRuleTileLayer.Void))
            map.SetTile((Vector3Int)placement.Cell, Load());
        map.RefreshAllTiles();
    }

    private static void WithMap(Action<Tilemap> action)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject root = null;
        try
        {
            root = new GameObject("AbyssSingleCellTest", typeof(Grid)) {hideFlags = HideFlags.HideAndDontSave};
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var child = new GameObject("Tiles", typeof(Tilemap)) {hideFlags = HideFlags.HideAndDontSave};
            child.transform.SetParent(root.transform, false);
            action(child.GetComponent<Tilemap>());
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
