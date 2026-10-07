using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CrystalMagic.Game.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SceneMapAssetTests
{
    private const string TownPath = "Assets/Res/Tile/OcclusionMaps/TownMap/TownMap_Occlusion.prefab";
    private const string TrainingPath = "Assets/Res/Tile/OcclusionMaps/TrainingMap/TrainingMap_Occlusion.prefab";

    [Test]
    public void TownDisplayAndPhysicsUseTheSameLocalCoordinateSystem()
    {
        GameObject display = AssetDatabase.LoadAssetAtPath<GameObject>(TownPath);
        Assert.That(display, Is.Not.Null);
        TileOcclusionMap map = display.GetComponent<TileOcclusionMap>();
        Assert.That(map.GeneratedRoot, Is.Not.Null);
        Assert.That(map.CollisionNeedsRebuild, Is.False);
        GameObject physics = map.CollisionPrefab;
        Assert.That(physics, Is.Not.Null);
        Assert.That(display.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(physics.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(display.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(physics.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(physics.GetComponentsInChildren<Renderer>(true), Is.Empty);
        Assert.That(physics.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(physics.GetComponentsInChildren<BoxCollider>(true).Length, Is.EqualTo(297));
        Assert.That(map.CollisionData.BlockedCells.Count, Is.EqualTo(1749));
        Assert.That(physics.GetComponent<TileCollisionNavigationAuthoring>().Data, Is.SameAs(map.CollisionData));
        Assert.That(display.GetComponentsInChildren<Collider>(true), Is.Empty);
        Assert.That(display.GetComponentsInChildren<Collider2D>(true).Any(c => c.enabled), Is.False);
        Assert.That(map.SourceRenderers.All(s => !s.Renderer.enabled), Is.True);
        Assert.That(map.GeneratedRoot.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.GreaterThan(4000));
    }

    [Test]
    public void TownSubSceneHasExactlyOneCollisionCompanionAndKeepsThePlayer()
    {
        string scene = File.ReadAllText("Assets/Scenes/SubScene/TownSubScene.unity");
        string reference = "m_SourcePrefab: {fileID: 100100000, guid: " + AssetDatabase.AssetPathToGUID(
            "Assets/Res/Tile/OcclusionMaps/TownMap/Collision/TownMap_Occlusion_Physics.prefab") + ", type: 3}";
        Assert.That(scene.Split(new[] { reference }, System.StringSplitOptions.None).Length - 1, Is.EqualTo(1));
        Assert.That(scene, Does.Contain("value: PlayerTown"));
        Assert.That(scene, Does.Not.Contain(AssetDatabase.AssetPathToGUID(TownPath)));
    }

    [Test]
    public void TrainingUsesProcessedOcclusionAndMatchingCollisionCompanion()
    {
        GameObject display = AssetDatabase.LoadAssetAtPath<GameObject>(TrainingPath);
        Assert.That(display, Is.Not.Null);
        var map = display.GetComponent<TileOcclusionMap>();
        Assert.That(map.NeedsRebuild, Is.False);
        Assert.That(map.CollisionNeedsRebuild, Is.False);
        Assert.That(map.Regions.Count, Is.EqualTo(31));
        Assert.That(map.GeneratedRoot.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(58));
        Assert.That(map.SourceRenderers.All(s => !s.Renderer.enabled), Is.True);
        Assert.That(display.GetComponentsInChildren<Collider>(true), Is.Empty);
        Assert.That(display.GetComponentsInChildren<Collider2D>(true).Any(c => c.enabled), Is.False);
        GameObject physics = map.CollisionPrefab;
        Assert.That(physics, Is.Not.Null);
        Assert.That(display.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(display.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(physics.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(physics.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(physics.GetComponentsInChildren<Renderer>(true), Is.Empty);
        Assert.That(physics.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(physics.GetComponentsInChildren<BoxCollider>(true).Length, Is.EqualTo(17));
        Assert.That(physics.GetComponent<TileCollisionNavigationAuthoring>().Data, Is.SameAs(map.CollisionData));
        var data = map.CollisionData;
        Assert.That(data.Width, Is.EqualTo(15));
        Assert.That(data.Height, Is.EqualTo(15));
        Assert.That(data.MinCorner, Is.EqualTo(new Vector3(0, -15, 0)));
        Assert.That(data.CellSize, Is.EqualTo(1));
        Assert.That(data.BlockedCells.Count, Is.EqualTo(54));
        CollectionAssert.AreEquivalent(map.Regions.SelectMany(r => r.CollisionCells).Distinct(), data.BlockedCells);
        CollectionAssert.AreEquivalent(map.Preset.Regions.SelectMany(r => r.CollisionCells).Distinct(), data.BlockedCells);
    }

    [Test]
    public void TrainingSubSceneBakesOnlyNewCollisionAndKeepsTheDummyAndPlayer()
    {
        string scene = File.ReadAllText("Assets/Scenes/SubScene/TrainingSubScene.unity");
        Assert.That(scene, Does.Not.Contain("f0c10247c282bab479c3a4beb7f454aa"));
        Assert.That(scene, Does.Not.Contain(AssetDatabase.AssetPathToGUID(TrainingPath)));
        var map = AssetDatabase.LoadAssetAtPath<GameObject>(TrainingPath).GetComponent<TileOcclusionMap>();
        Assert.That(map.CollisionPrefab, Is.Not.Null);
        string reference = "m_SourcePrefab: {fileID: 100100000, guid: " + AssetDatabase.AssetPathToGUID(
            AssetDatabase.GetAssetPath(map.CollisionPrefab)) + ", type: 3}";
        Assert.That(scene.Split(new[] { reference }, System.StringSplitOptions.None).Length - 1, Is.EqualTo(1));
        Assert.That(scene, Does.Contain("value: MonsterStraw"));
        Assert.That(scene, Does.Contain("value: Player"));
    }

    [TestCase(TownPath, "TownSubScene", "PlayerTown")]
    [TestCase(TrainingPath, "TrainingSubScene", "Player")]
    [TestCase(TrainingPath, "TrainingSubScene", "MonsterStraw")]
    public void SavedUnitPositionIsInsideTheMapAndNotBlocked(string mapPath, string sceneName, string unitName)
    {
        string scene = File.ReadAllText("Assets/Scenes/SubScene/" + sceneName + ".unity");
        string player = Regex.Split(scene, @"(?m)^--- !u!1001").Single(block =>
            Regex.IsMatch(block, @"(?m)^\s+value: " + Regex.Escape(unitName) + @"\r?$"));
        float ReadAxis(string axis)
        {
            Match match = Regex.Match(player, @"propertyPath: m_LocalPosition\." + axis + @"\s+value: ([^\r\n]+)");
            Assert.That(match.Success, Is.True, unitName + " position must be saved in the SubScene.");
            return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
        Vector3 spawn = new Vector3(ReadAxis("x"), ReadAxis("y"), ReadAxis("z"));
        TileCollisionData mask = AssetDatabase.LoadAssetAtPath<GameObject>(mapPath).GetComponent<TileOcclusionMap>().CollisionData;
        Vector3Int cell = Vector3Int.FloorToInt((spawn - mask.GridOrigin) / mask.CellSize);
        cell.z = 0;
        Assert.That(cell.x, Is.InRange(mask.MinCell.x, mask.MinCell.x + mask.Width - 1));
        Assert.That(cell.y, Is.InRange(mask.MinCell.y, mask.MinCell.y + mask.Height - 1));
        CollectionAssert.DoesNotContain(mask.BlockedCells, cell);
    }

    [TestCase(TownPath)]
    [TestCase(TrainingPath)]
    public void RealMapHasFourSolidWallsOutsidePlayableCells(string path)
    {
        var map = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<TileOcclusionMap>();
        var walls = map.CollisionPrefab.GetComponentsInChildren<BoxCollider>()
            .Where(c => c.name.StartsWith("Boundary_")).ToArray();
        Assert.That(walls.Length, Is.EqualTo(4));
        Rect area = map.CollisionData.LocalBounds;
        foreach (var wall in walls)
        {
            Assert.That(wall.enabled && !wall.isTrigger, Is.True);
            Bounds box = new Bounds(wall.transform.localPosition + wall.center, wall.size);
            Assert.That(box.min.x >= area.xMax || box.max.x <= area.xMin ||
                box.min.y >= area.yMax || box.max.y <= area.yMin, Is.True, "Do not consume a playable edge tile.");
        }
        // Walk the entire perimeter, including corners. Every immediately-outside
        // point must be inside a solid collider; original in-map footprints stay unchanged.
        for (int y = -1; y <= map.CollisionData.Height; y++)
            for (int x = -1; x <= map.CollisionData.Width; x++)
            {
                if (x >= 0 && x < map.CollisionData.Width && y >= 0 && y < map.CollisionData.Height) continue;
                Vector3 point = map.CollisionData.MinCorner + new Vector3(x + .5f, y + .5f, 0) * map.CollisionData.CellSize;
                Assert.That(walls.Any(w => new Bounds(w.transform.localPosition + w.center, w.size).Contains(point)), Is.True, $"{x},{y}");
            }
    }
}
