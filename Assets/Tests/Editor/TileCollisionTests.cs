using System;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Editor.Map;
using CrystalMagic.Game.Map;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using BoxCollider = UnityEngine.BoxCollider;

public sealed class TileCollisionTests
{
    private Scene _scene;
    private GameObject _root;
    private string _folder;
    private int _undo;

    [SetUp]
    public void SetUp()
    {
        Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup(); _undo = Undo.GetCurrentGroup();
        _scene = EditorSceneManager.NewPreviewScene();
        string name = "__TileCollisionTests_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", name); _folder = "Assets/" + name;
    }

    [TearDown]
    public void TearDown()
    {
        Undo.RevertAllDownToGroup(_undo);
        if (_root != null) Object.DestroyImmediate(_root);
        if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        AssetDatabase.DeleteAsset(_folder);
    }

    private TileOcclusionMap LoadTown()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/Tile/TownMap.tmx");
        _root = (GameObject)PrefabUtility.InstantiatePrefab(source, _scene);
        PrefabUtility.UnpackPrefabInstance(_root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var map = _root.AddComponent<TileOcclusionMap>();
        TileOcclusionAutoGrouper.Apply(map);
        TileCollisionBuilder.ApplyRules(map);
        return map;
    }

    [Test]
    public void TownTreesWellsFramesAndPropsHaveIndependentFootprints()
    {
        var map = LoadTown();
        Assert.That(map.Regions.Count, Is.EqualTo(386));
        var trees = map.Regions.Where(r => r.Category == "树木").ToArray();
        Assert.That(trees.Count(r => r.CollisionCells.Count == 2), Is.GreaterThan(100));
        foreach (var tree in trees.Where(r => r.CollisionCells.Count == 2))
        {
            Assert.That(tree.CollisionCells.Select(c => c.x).Distinct().Count(), Is.EqualTo(1));
            Assert.That(tree.CollisionCells.Max(c => c.y) - tree.CollisionCells.Min(c => c.y), Is.EqualTo(1));
            Assert.That(tree.Cells.Count, Is.GreaterThan(tree.CollisionCells.Count));
            foreach (var trunk in tree.Cells.Where(c => c.Source.GetSprite(c.Position).texture.name == "Village_Tileset"))
            {
                var sprite = trunk.Source.GetSprite(trunk.Position);
                int col = Mathf.RoundToInt(sprite.rect.x / sprite.rect.width);
                int row = Mathf.RoundToInt((sprite.texture.height - sprite.rect.yMax) / sprite.rect.height);
                if (col == 1 && (row == 13 || row == 14)) Assert.That(tree.CollisionCells.Contains(trunk.Position), Is.True);
                if (col == 1 && row == 15) Assert.That(tree.CollisionCells.Contains(trunk.Position), Is.False, "树根阴影格不能占掉前方草地");
            }
        }
        var wells = map.Regions.Where(r => r.CollisionMode == TileCollisionMode.WellBase).ToArray();
        Assert.That(wells.Length, Is.GreaterThan(0));
        foreach (var well in wells)
        {
            Assert.That(well.CollisionCells.Count, Is.EqualTo(2));
            Assert.That(well.CollisionCells.All(c => c.y == well.Cells.Min(t => t.Position.y)), Is.True);
        }
        foreach (var house in map.Regions.Where(r => r.Category == "房屋"))
        {
            Assert.That(house.CollisionCells.Count, Is.GreaterThan(10));
            Assert.That(house.CollisionCells.Distinct().Count(), Is.EqualTo(house.CollisionCells.Count));
        }
        foreach (var full in map.Regions.Where(r => r.CollisionMode == TileCollisionMode.Full))
            Assert.That(full.CollisionCells, Is.EquivalentTo(full.Cells.Select(c => c.Position).Distinct()));
    }

    [Test]
    public void HousesFillTheirClosedGroundPlansAndIgnoreRoofAndFacadeLayers()
    {
        var map = LoadTown();
        Vector3Int[] Rect(int x0, int x1, int row0, int row1) => Enumerable.Range(row0, row1 - row0 + 1)
            .SelectMany(row => Enumerable.Range(x0, x1 - x0 + 1).Select(x => new Vector3Int(x, -row, 0))).ToArray();
        // Reviewed against the ground-plan layer, not the rendered roof outline.
        var expected = new[]
        {
            Rect(31, 41, 3, 8).Concat(Rect(34, 40, 9, 9)),
            Rect(53, 64, 7, 12).Concat(Rect(54, 60, 13, 13)),
            Rect(73, 84, 13, 20),
            Rect(6, 13, 15, 20).Concat(Rect(8, 12, 21, 21)),
            Rect(34, 40, 16, 21),
            Rect(18, 28, 20, 26).Concat(Rect(18, 24, 27, 29)),
            Rect(51, 67, 22, 34).Concat(Rect(51, 57, 35, 36)).Concat(Rect(61, 67, 35, 36)),
            Rect(72, 83, 33, 40),
            Rect(2, 14, 36, 43),
            Rect(21, 29, 40, 41).Concat(Rect(20, 38, 42, 51)).Concat(Rect(21, 29, 52, 52))
        };
        var houses = map.Regions.Where(r => r.Category == "房屋").OrderBy(r => r.Name).ToArray();
        Assert.That(houses.Length, Is.EqualTo(expected.Length));
        var grid = TileCollisionBuilder.ReferenceGrid(map);
        for (int i = 0; i < houses.Length; i++)
        {
            Assert.That(houses[i].CollisionCells, Is.EquivalentTo(expected[i]), houses[i].Name);
            houses[i].Cells.RemoveAll(c => c.Source != grid);
        }
        TileCollisionBuilder.ApplyRules(map);
        for (int i = 0; i < houses.Length; i++)
            Assert.That(houses[i].CollisionCells, Is.EquivalentTo(expected[i]), "移除屋顶/门窗图层不能改变房屋占地");
    }

    [Test]
    public void BrokenConcaveFramesCloseLocallyWithoutBridgingTheirCourtyard()
    {
        // U-shaped ground plan, with all four lower corners missing and a
        // three-cell doorway gap in its top wall.
        var expected = Enumerable.Range(0, 9).SelectMany(y => Enumerable.Range(0, 11)
            .Where(x => y >= 4 || x <= 3 || x >= 7).Select(x => new Vector3Int(x, y, 0))).ToHashSet();
        var directions = new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };
        var frame = expected.Where(p => directions.Any(d => !expected.Contains(p + d))).ToHashSet();
        foreach (int x in new[] { 0, 3, 7, 10 }) frame.Remove(new Vector3Int(x, 0, 0));
        foreach (int x in new[] { 4, 5, 6 }) frame.Remove(new Vector3Int(x, 8, 0));
        Assert.That(TileHouseFootprintBuilder.Build(frame), Is.EquivalentTo(expected));
        var offset = new Vector3Int(-20, -30, 0);
        Assert.That(TileHouseFootprintBuilder.Build(frame.Reverse().Select(p => p + offset)),
            Is.EquivalentTo(expected.Select(p => p + offset)), "输入顺序和负坐标不改变闭合结果");
    }

    [Test]
    public void IncompleteHouseWithoutEnoughWallEvidenceIsRejected()
    {
        var open = Enumerable.Range(0, 7).Select(x => new Vector3Int(x, 6, 0))
            .Concat(Enumerable.Range(0, 6).SelectMany(y => new[] { new Vector3Int(0, y, 0), new Vector3Int(6, y, 0) }));
        Assert.Throws<InvalidOperationException>(() => TileHouseFootprintBuilder.Build(open));
    }

    [Test]
    public void StumpsOnlyBlockTheirOwnCellNotTheFourFringeCells()
    {
        var map = LoadTown();
        var stumps = map.Regions.Where(r => r.Category == "木桩").ToArray();
        Assert.That(stumps.Length, Is.EqualTo(44));
        foreach (var stump in stumps)
        {
            Assert.That(stump.CollisionMode, Is.EqualTo(TileCollisionMode.StumpCore));
            var core = stump.Cells.Where(c =>
            {
                var sprite = c.Source.GetSprite(c.Position);
                int col = Mathf.RoundToInt(sprite.rect.x / sprite.rect.width);
                int row = Mathf.RoundToInt((sprite.texture.height - sprite.rect.yMax) / sprite.rect.height);
                int id = row * 32 + col;
                return sprite.texture.name == "Village_Tileset" && (id == 449 || id == 416 || id == 418 || id == 480 || id == 482);
            }).Select(c => c.Position).Distinct().ToArray();
            Assert.That(stump.CollisionCells, Is.EquivalentTo(core), stump.Name);
            Assert.That(stump.CollisionCells.Count, Is.LessThanOrEqualTo(1), stump.Name);
        }
        Assert.That(stumps.Count(r => r.CollisionCells.Count == 1), Is.EqualTo(40));
        Assert.That(stumps.Count(r => r.CollisionCells.Count == 0), Is.EqualTo(4));
        // Map-edge fringe fragments do not prevent exporting the rest of the map.
        var data = TileCollisionBuilder.CreateData(map);
        Object.DestroyImmediate(data);
    }

    [Test]
    public void ManualCollisionSurvivesRegroupPresetAndUndoWithoutChangingVisualCells()
    {
        var map = LoadTown(); var region = map.Regions.First(r => r.Category == "树木" && r.CollisionCells.Count == 2);
        var visual = region.Cells.ToArray();
        var grid = TileCollisionBuilder.ReferenceGrid(map);
        Vector3 point = grid.GetCellCenterWorld(region.CollisionCells[0]);
        TileCollisionBuilder.Paint(map, region, point, point, false);
        Assert.That(region.CollisionCells.Count, Is.EqualTo(1));
        Assert.That(region.Cells, Is.EquivalentTo(visual));
        TileOcclusionAutoGrouper.Apply(map); TileCollisionBuilder.ApplyRules(map);
        Assert.That(region.CollisionCells.Count, Is.EqualTo(1));
        Assert.That(map.Regions.Contains(region), Is.True);
        var preset = TileOcclusionTmxWorkflow.SavePreset(map, null, _folder + "/Preset.asset", "test");
        TileOcclusionTmxWorkflow.LoadPreset(map, preset);
        var restored = map.Regions.Single(r => r.Name == region.Name && !r.AutoGenerated);
        Assert.That(restored.CollisionMode, Is.EqualTo(TileCollisionMode.Manual));
        Assert.That(restored.CollisionCells, Is.EquivalentTo(region.CollisionCells));
        Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
        TileCollisionBuilder.Paint(map, restored, point, point, true);
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
        Assert.That(map.Regions.Single(r => r.Name == region.Name && !r.AutoGenerated).CollisionCells.Count, Is.EqualTo(1));
    }

    [Test]
    public void RectangleMergeNeverFillsCourtyardHolesOrAddsDuplicateCells()
    {
        var cells = Enumerable.Range(0, 5).SelectMany(y => Enumerable.Range(0, 7).Where(x => x == 0 || x == 6 || y == 0 || y == 4)
            .Select(x => new Vector3Int(x, y, 0))).ToArray();
        var rects = TileCollisionBuilder.MergeRectangles(cells.Concat(cells));
        var expanded = rects.SelectMany(r => Enumerable.Range(r.y, r.height).SelectMany(y =>
            Enumerable.Range(r.x, r.width).Select(x => new Vector3Int(x, y, 0)))).ToArray();
        Assert.That(expanded, Is.EquivalentTo(cells));
        Assert.That(rects.Count, Is.LessThan(cells.Length));
    }

    [Test]
    public void ExportedPrefabAndNavigationUseExactlySameTownMask()
    {
        var map = LoadTown();
        var prefab = TileCollisionBuilder.Export(map, _folder + "/Collision");
        var data = map.CollisionData;
        Assert.That(prefab.GetComponentsInChildren<Renderer>().Length, Is.Zero);
        Assert.That(prefab.GetComponentsInChildren<Collider2D>().Length, Is.Zero);
        var words = TileCollisionNavigationAuthoring.BuildWords(data);
        for (int y = 0; y < data.Height; y++) for (int x = 0; x < data.Width; x++)
        {
            int index = y * data.Width + x;
            bool blocked = data.BlockedCells.Contains(new Vector3Int(x + data.MinCell.x, y + data.MinCell.y, 0));
            Assert.That((words[index >> 6] & (1UL << (index & 63))) != 0, Is.EqualTo(blocked));
            Vector3 point = data.MinCorner + new Vector3(x + .5f, y + .5f, 0) * data.CellSize;
            int covering = prefab.GetComponentsInChildren<BoxCollider>().Count(c =>
                new Bounds(c.transform.localPosition + c.center, c.size).Contains(point));
            Assert.That(covering, Is.EqualTo(blocked ? 1 : 0), $"{x},{y}");
        }
        Assert.That(map.CollisionNeedsRebuild, Is.False);
    }

    [Test]
    public void NavigationRejectsRotatedOrInvalidFootprints()
    {
        var data = ScriptableObject.CreateInstance<TileCollisionData>();
        _root = new GameObject("Validation"); SceneManager.MoveGameObjectToScene(_root, _scene);
        data.Width = data.Height = 5;
        try
        {
            _root.transform.rotation = Quaternion.Euler(0, 0, 15);
            Assert.Throws<InvalidOperationException>(() => TileCollisionNavigationAuthoring.Describe(data, _root.transform));
            _root.transform.rotation = Quaternion.identity;
            data.BlockedCells.Add(new Vector3Int(5, 0, 0));
            Assert.Throws<InvalidOperationException>(() => TileCollisionNavigationAuthoring.BuildWords(data));
        }
        finally { Object.DestroyImmediate(data); }
    }

    [Test]
    public void CompanionActuallyBakesEcsPhysicsAndNavigation()
    {
        var data = ScriptableObject.CreateInstance<TileCollisionData>();
        data.Width = data.Height = 4; data.GridOrigin = new Vector3(-2, -2, 0);
        data.BlockedCells.AddRange(new[] { new Vector3Int(1, 1, 0), new Vector3Int(1, 2, 0) });
        AssetDatabase.CreateAsset(data, _folder + "/Data.asset");
        _root = new GameObject("BakeCollision"); SceneManager.MoveGameObjectToScene(_root, _scene);
        TileCollisionBuilder.PopulateCompanion(_root, data, 0);
        using var world = new World("Collision bake regression", WorldFlags.Game);
        using var blobs = new BlobAssetStore(128);
        // Unity exposes full baking to its package tests internally. Use that
        // actual pipeline through reflection here rather than mock its result.
        Assembly assembly = typeof(Baker<>).Assembly;
        Type utility = assembly.GetType("Unity.Entities.BakingUtility", true);
        Type settingsType = assembly.GetType("Unity.Entities.BakingSettings", true);
        object settings = Activator.CreateInstance(settingsType);
        settingsType.GetProperty("BlobAssetStore").SetValue(settings, blobs);
        utility.GetMethod("BakeGameObjects", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { world, new[] { _root }, settings });
        using var navigation = world.EntityManager.CreateEntityQuery(typeof(DungeonNavigationMapComponent), typeof(DungeonNavigationCollisionWord));
        Assert.That(navigation.CalculateEntityCount(), Is.EqualTo(1));
        var nav = navigation.GetSingleton<DungeonNavigationMapComponent>();
        var buffer = world.EntityManager.GetBuffer<DungeonNavigationCollisionWord>(navigation.GetSingletonEntity());
        Assert.That(DungeonNavigationMapUtility.IsBlocked(nav, buffer.AsNativeArray(), new int2(1, 1)), Is.True);
        Assert.That(DungeonNavigationMapUtility.IsBlocked(nav, buffer.AsNativeArray(), new int2(0, 1)), Is.False);
        using var physics = world.EntityManager.CreateEntityQuery(typeof(PhysicsCollider), typeof(LocalToWorld));
        using var entities = physics.ToEntityArray(Allocator.Temp);
        Assert.That(entities.Length, Is.GreaterThan(0));
        bool Hit(float3 point)
        {
            foreach (var entity in entities)
            {
                var body = new RigidBody
                {
                    Collider = world.EntityManager.GetComponentData<PhysicsCollider>(entity).Value,
                    WorldFromBody = new RigidTransform(world.EntityManager.GetComponentData<LocalToWorld>(entity).Value),
                    Scale = 1, Entity = entity
                };
                if (body.CastRay(new RaycastInput { Start = point + new float3(0, 0, -2), End = point + new float3(0, 0, 2), Filter = CollisionFilter.Default })) return true;
            }
            return false;
        }
        Assert.That(Hit(new float3(-.5f, -.5f, 0)), Is.True, "树干格应有实际 ECS 碰撞体");
        Assert.That(Hit(new float3(.5f, -.5f, 0)), Is.False, "树干旁边不得有碰撞");
        foreach (float3 outside in new[] { new float3(-2.5f, 0, 0), new float3(2.5f, 0, 0),
            new float3(0, -2.5f, 0), new float3(0, 2.5f, 0), new float3(-2.5f, -2.5f, 0), new float3(2.5f, 2.5f, 0) })
            Assert.That(Hit(outside), Is.True, "地图外圈应烘焙成实际 ECS 碰撞体");
        Assert.That(DungeonNavigationMapUtility.IsBlocked(nav, buffer.AsNativeArray(), new int2(-1, 0)), Is.True);
        Assert.That(DungeonNavigationMapUtility.IsBlocked(nav, buffer.AsNativeArray(), new int2(4, 0)), Is.True);
    }
}
