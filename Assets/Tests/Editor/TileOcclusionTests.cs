using System;
using System.Linq;
using CrystalMagic.Editor.Map;
using CrystalMagic.Game.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public sealed class TileOcclusionTests
{
    private Scene _scene, _previousScene;
    private GameObject _root;
    private TileOcclusionMap _map;
    private Tilemap _tiles;
    private Tile _tile;
    private Sprite _sprite;
    private Texture2D _texture;
    private Material _material;
    private string _assetFolder;

    [SetUp]
    public void SetUp()
    {
        _previousScene = SceneManager.GetActiveScene();
        _scene = EditorSceneManager.NewPreviewScene();
        _root = new GameObject("OcclusionTestMap", typeof(Grid), typeof(TileOcclusionMap));
        SceneManager.MoveGameObjectToScene(_root, _scene);
        _map = _root.GetComponent<TileOcclusionMap>();
        _texture = new Texture2D(16, 16);
        _texture.SetPixels(Enumerable.Repeat(Color.white, 256).ToArray());
        _texture.Apply();
        _sprite = Sprite.Create(_texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
        _tile = ScriptableObject.CreateInstance<Tile>();
        _tile.sprite = _sprite;
        _tile.colliderType = Tile.ColliderType.Grid;
        _material = new Material(Shader.Find("Sprites/Default"));
        string folderName = "__TileOcclusionTests_" + Guid.NewGuid().ToString("N");
        _assetFolder = "Assets/" + folderName;
        AssetDatabase.CreateFolder("Assets", folderName);
        AssetDatabase.CreateAsset(_tile, _assetFolder + "/Tile.asset");
        AssetDatabase.AddObjectToAsset(_sprite, _tile);
        AssetDatabase.AddObjectToAsset(_texture, _tile);
        AssetDatabase.CreateAsset(_material, _assetFolder + "/Material.mat");
        AssetDatabase.SaveAssets();
        _tiles = CreateLayer("Trees");
        _tiles.SetTile(Vector3Int.zero, _tile);
        _tiles.SetTile(Vector3Int.up, _tile);
        _tiles.SetTile(Vector3Int.right * 3, _tile);
        _tiles.gameObject.AddComponent<TilemapCollider2D>();
        TileOcclusionBuilder.SyncLayers(_map);
        _map.Layers[0].Selectable = true;
        _map.Regions.Add(new TileOcclusionRegion { Name = "Tree", LocalAnchor = new Vector3(0.5f, 0, 0) });
        _map.Regions[0].Cells.Add(new TileOcclusionCell(_tiles, Vector3Int.zero));
        _map.Regions[0].Cells.Add(new TileOcclusionCell(_tiles, Vector3Int.up));
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null) Object.DestroyImmediate(_root);
        if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        if (_previousScene.IsValid() && _previousScene.isLoaded) SceneManager.SetActiveScene(_previousScene);
        foreach (Object asset in new Object[] { _tile, _sprite, _texture, _material })
            if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
        if (!string.IsNullOrEmpty(_assetFolder) && AssetDatabase.IsValidFolder(_assetFolder))
            AssetDatabase.DeleteAsset(_assetFolder);
    }

    private Tilemap CreateLayer(string name)
    {
        var obj = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        obj.transform.SetParent(_root.transform, false);
        obj.GetComponent<TilemapRenderer>().sharedMaterial = _material;
        return obj.GetComponent<Tilemap>();
    }

    private Tilemap Remainder() => _map.GeneratedRoot.GetComponentsInChildren<Tilemap>()
        .Single(t => t.name == _tiles.name);

    [Test]
    public void GeneratePreservesSourceDataAndCollisionAndMasksOnlyCopy()
    {
        _tiles.gameObject.layer = 2;
        _tiles.GetComponent<TilemapRenderer>().renderingLayerMask = 2;
        _tiles.SetTileFlags(Vector3Int.zero, TileFlags.None);
        _tiles.SetColor(Vector3Int.zero, new Color(0.4f, 0.7f, 0.8f, 0.6f));
        Color original = _tiles.GetColor(Vector3Int.zero);
        TileFlags flags = _tiles.GetTileFlags(Vector3Int.zero);
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        Assert.That(_tiles.GetTile(Vector3Int.zero), Is.SameAs(_tile));
        Assert.That(_tiles.GetColor(Vector3Int.zero), Is.EqualTo(original));
        Assert.That(_tiles.GetTileFlags(Vector3Int.zero), Is.EqualTo(flags));
        Assert.That(_tiles.GetComponent<TilemapCollider2D>().enabled, Is.True);
        Assert.That(_tiles.GetComponent<TilemapRenderer>().enabled, Is.False);
        Assert.That(_map.GeneratedRoot.GetComponentsInChildren<Collider2D>(), Is.Empty);
        Assert.That(_map.GeneratedRoot.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(2));
        Assert.That(_map.GeneratedRoot.GetComponentsInChildren<SpriteRenderer>().All(r => r.gameObject.layer == 2 && r.renderingLayerMask == 2), Is.True);
        Assert.That(Remainder().gameObject.layer, Is.EqualTo(2));
        Assert.That(Remainder().GetComponent<TilemapRenderer>().renderingLayerMask, Is.EqualTo(2));
        Assert.That(Remainder().GetSprite(Vector3Int.right * 3), Is.SameAs(_sprite));
        Assert.That(Remainder().HasTile(Vector3Int.zero), Is.False);
        Assert.That(Remainder().HasTile(Vector3Int.up), Is.False);
        Assert.That(Remainder().GetColor(Vector3Int.right * 3).a, Is.EqualTo(1));
    }

    [Test]
    public void MaskSurvivesTileRefresh()
    {
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        Remainder().RefreshAllTiles();
        Assert.That(Remainder().HasTile(Vector3Int.zero), Is.False);
        Assert.That(Remainder().GetColor(Vector3Int.right * 3).a, Is.EqualTo(1));
    }

    [Test]
    public void FrontAndBackUnitsStraddleTheWholeGroup()
    {
        _map.Regions[0].LocalAnchor = new Vector3(0.5f, 2, 0);
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        SortingGroup group = _map.GeneratedRoot.GetComponentInChildren<SortingGroup>();
        Assert.That(group.sortingOrder, Is.EqualTo(-200));
        Assert.That(TileOcclusionSortAnchor.OrderForY(1.5f), Is.GreaterThan(group.sortingOrder));
        Assert.That(TileOcclusionSortAnchor.OrderForY(2.5f), Is.LessThan(group.sortingOrder));
        Assert.That(Remainder().GetComponent<TilemapRenderer>().sortingOrder, Is.LessThan(-29999));
    }

    [Test]
    public void RenderedPixelsShowUnitInFrontAndObstacleBehindItsFootLine()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || GraphicsSettings.currentRenderPipeline != null)
            Assert.Ignore("需要带图形设备的 Built-in 测试场景；URP 正式场景另行集成验证。");
        _map.Regions[0].LocalAnchor = new Vector3(0.5f, 2, 0);
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        var unit = new GameObject("Unit", typeof(SpriteRenderer));
        unit.transform.SetParent(_root.transform);
        unit.transform.position = new Vector3(0.5f, 0.5f, 0);
        var renderer = unit.GetComponent<SpriteRenderer>();
        renderer.sprite = _sprite;
        renderer.sharedMaterial = _material;
        renderer.color = Color.magenta;
        var cameraObject = new GameObject("TestCamera", typeof(Camera));
        cameraObject.transform.SetParent(_root.transform);
        cameraObject.transform.position = new Vector3(0.5f, 0.5f, -10);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = _scene;
        camera.orthographic = true;
        camera.orthographicSize = 0.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        var target = new RenderTexture(16, 16, 24);
        var pixels = new Texture2D(16, 16);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            SortingGroup.UpdateAllSortingGroups();
            renderer.sortingOrder = TileOcclusionSortAnchor.OrderForY(1.5f);
            Color front = Sample();
            Assert.That(front.r, Is.GreaterThan(0.9f));
            Assert.That(front.b, Is.GreaterThan(0.9f));
            Assert.That(front.g, Is.LessThan(0.1f), "人物在落地点前方时应该覆盖整组图块");
            renderer.sortingOrder = TileOcclusionSortAnchor.OrderForY(2.5f);
            Color behind = Sample();
            Assert.That(behind.g, Is.GreaterThan(0.9f), "人物在落地点后方时应该被白色障碍遮住");
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
        }

        Color Sample()
        {
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            pixels.Apply();
            return pixels.GetPixel(8, 8);
        }
    }

    [Test]
    public void RebuildDoesNotDuplicateAndRestoreRecoversOriginalVisibility()
    {
        Tilemap disabled = CreateLayer("OriginallyHidden");
        disabled.SetTile(Vector3Int.zero, _tile);
        disabled.GetComponent<TilemapRenderer>().enabled = false;
        TileOcclusionBuilder.SyncLayers(_map);
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        Assert.That(_map.GetComponentsInChildren<TileOcclusionSortAnchor>().Length, Is.EqualTo(1));
        Assert.That(_map.SourceRenderers.Count, Is.EqualTo(2));
        TileOcclusionBuilder.Restore(_map);
        Assert.That(_map.GeneratedRoot == null, Is.True);
        Assert.That(_map.SourceRenderers, Is.Empty);
        Assert.That(_tiles.GetComponent<TilemapRenderer>().enabled, Is.True);
        Assert.That(disabled.GetComponent<TilemapRenderer>().enabled, Is.False);
        Assert.That(_map.Regions[0].Cells.Count, Is.EqualTo(2));
    }

    [Test]
    public void UndoAndRedoGenerationRetainRecoverableState()
    {
        Undo.FlushUndoRecordObjects();
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.That(_map.GeneratedRoot == null, Is.True);
        Assert.That(_map.SourceRenderers, Is.Empty);
        Assert.That(_tiles.GetComponent<TilemapRenderer>().enabled, Is.True);
        Undo.PerformRedo();
        Assert.That(_map.GeneratedRoot, Is.Not.Null);
        Assert.That(_map.SourceRenderers.Count, Is.EqualTo(1));
        Assert.That(_tiles.GetComponent<TilemapRenderer>().enabled, Is.False);
        Assert.That(_map.GeneratedRoot.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(2));
        Assert.That(Remainder().HasTile(Vector3Int.zero), Is.False);
        Assert.That(Remainder().GetSprite(Vector3Int.right * 3), Is.SameAs(_sprite));
        TileOcclusionBuilder.Restore(_map);
        Assert.That(_tiles.GetComponent<TilemapRenderer>().enabled, Is.True);
    }

    [Test]
    public void RebuildAndRestoreCanBeUndoneWithoutLosingOriginalGeneration()
    {
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        Tile firstSnapshot = _map.SnapshotAsset;
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        Tile secondSnapshot = _map.SnapshotAsset;
        Assert.That(secondSnapshot, Is.Not.SameAs(firstSnapshot));
        Undo.PerformUndo();
        Assert.That(_map.SnapshotAsset, Is.SameAs(firstSnapshot));
        Assert.That(_map.GetComponentsInChildren<TileOcclusionSortAnchor>().Length, Is.EqualTo(1));
        Undo.PerformRedo();
        Assert.That(_map.SnapshotAsset, Is.SameAs(secondSnapshot));
        Assert.That(_map.GetComponentsInChildren<TileOcclusionSortAnchor>().Length, Is.EqualTo(1));
        TileOcclusionBuilder.Restore(_map);
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.That(_map.SnapshotAsset, Is.SameAs(secondSnapshot));
        Assert.That(_map.GeneratedRoot != null, Is.True);
        Assert.That(_tiles.GetComponent<TilemapRenderer>().enabled, Is.False);
    }

    [Test]
    public void NeighbourDependentTileKeepsResolvedAppearanceAfterSelectionIsRemoved()
    {
        var neighbourTile = ScriptableObject.CreateInstance<TileOcclusionNeighbourTestTile>();
        neighbourTile.Sprite = _sprite;
        try
        {
            _tiles.SetTile(Vector3Int.right, neighbourTile);
            _tiles.RefreshAllTiles();
            Assert.That(_tiles.GetColor(Vector3Int.right), Is.EqualTo(Color.red));
            TileOcclusionBuilder.Generate(_map, _assetFolder);
            Assert.That(Remainder().HasTile(Vector3Int.zero), Is.False);
            Remainder().RefreshAllTiles();
            Assert.That(Remainder().GetColor(Vector3Int.right), Is.EqualTo(Color.red));
            Assert.That(_tiles.GetTile(Vector3Int.right), Is.SameAs(neighbourTile));
        }
        finally { Object.DestroyImmediate(neighbourTile); }
    }

    [Test]
    public void OverlapIsSkippedAndManuallyCorruptDuplicatesFailBeforeRebuild()
    {
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        Transform generated = _map.GeneratedRoot;
        _map.Regions.Add(new TileOcclusionRegion { Name = "Duplicate" });
        int conflicts = TileOcclusionBuilder.AddCells(_map, 1, _map.Regions[0].Cells.ToArray());
        Assert.That(conflicts, Is.EqualTo(2));
        _map.Regions[1].Cells.Add(_map.Regions[0].Cells[0]);
        Assert.Throws<InvalidOperationException>(() => TileOcclusionBuilder.Generate(_map, _assetFolder));
        Assert.That(_map.GeneratedRoot, Is.SameAs(generated));
        Assert.That(_tiles.GetComponent<TilemapRenderer>().enabled, Is.False);
    }

    [Test]
    public void RectangleOnlySelectsEnabledSourceLayersAndExistingCells()
    {
        Tilemap ground = CreateLayer("Ground");
        ground.SetTile(Vector3Int.zero, _tile);
        TileOcclusionBuilder.SyncLayers(_map);
        var selected = TileOcclusionBuilder.CollectRectangle(_map, new Vector3(0.1f, 0.1f), new Vector3(1.9f, 1.9f));
        Assert.That(selected.Count, Is.EqualTo(2));
        Assert.That(selected.All(c => c.Source == _tiles), Is.True);
    }

    [Test]
    public void FlippedTileTransformsAndColoursMatchAndAnchorFollowsParent()
    {
        _root.transform.position = new Vector3(8, 12, 0);
        _root.transform.localScale = Vector3.one * 2;
        _tiles.SetTileFlags(Vector3Int.zero, TileFlags.None);
        _tiles.SetTransformMatrix(Vector3Int.zero, Matrix4x4.TRS(new Vector3(0.1f, 0.2f, 0), Quaternion.Euler(0, 0, 90), new Vector3(-1, 1, 1)));
        _tiles.color = new Color(0.5f, 1, 0.5f, 0.7f);
        Matrix4x4 expected = TileOcclusionBuilder.CellMatrix(_map.Regions[0].Cells[0]);
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        SpriteRenderer renderer = _map.GeneratedRoot.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Trees_0_0");
        Matrix4x4 actual = renderer.transform.localToWorldMatrix;
        for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
                Assert.That(actual[row, column], Is.EqualTo(expected[row, column]).Within(0.0001f));
        Assert.That(renderer.color, Is.EqualTo(_tiles.color * _tiles.GetColor(Vector3Int.zero)));
        _root.transform.position += Vector3.up * 2;
        TileOcclusionSortAnchor anchor = _map.GeneratedRoot.GetComponentInChildren<TileOcclusionSortAnchor>();
        anchor.Refresh();
        Assert.That(anchor.GetComponent<SortingGroup>().sortingOrder, Is.EqualTo(-1400));
    }

    [Test]
    public void ForeignOrRenamedGeneratedRootIsNeverDestroyed()
    {
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        Transform generated = _map.GeneratedRoot;
        generated.name = "User renamed this";
        Assert.Throws<InvalidOperationException>(() => TileOcclusionBuilder.Restore(_map));
        Assert.That(generated, Is.Not.Null);
        Assert.That(_map.SourceRenderers.Count, Is.EqualTo(1));
        Assert.That(_tiles.GetComponent<TilemapRenderer>().enabled, Is.False);
    }

    [Test]
    public void SourceLayersRefreshNeverIncludesGeneratedCopies()
    {
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        TileOcclusionBuilder.SyncLayers(_map);
        Assert.That(_map.Layers.Count, Is.EqualTo(1));
        Assert.That(_map.Layers[0].Source, Is.SameAs(_tiles));
    }

    [Test]
    public void ExportedPrefabRetainsSpritesMasksAnchorsAndRestoreReferences()
    {
        TileOcclusionBuilder.Generate(_map, _assetFolder);
        string path = _assetFolder + "/Map.prefab";
        Assert.That(PrefabUtility.SaveAsPrefabAsset(_root, path), Is.Not.Null);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            TileOcclusionMap loaded = contents.GetComponent<TileOcclusionMap>();
            Assert.That(loaded.GeneratedRoot, Is.Not.Null);
            Assert.That(loaded.SourceRenderers[0].Renderer.transform.IsChildOf(loaded.transform), Is.True);
            Assert.That(loaded.GeneratedRoot.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(2));
            Assert.That(loaded.GeneratedRoot.GetComponentsInChildren<SpriteRenderer>().All(s => s.sprite != null), Is.True);
            Tilemap remaining = loaded.GeneratedRoot.GetComponentInChildren<Tilemap>();
            remaining.RefreshAllTiles();
            Assert.That(remaining.HasTile(Vector3Int.zero), Is.False);
            Assert.That(remaining.GetColor(Vector3Int.right * 3).a, Is.EqualTo(1));
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }
}

// A dependency-free RuleTile analogue: the left neighbour affects its output.
public sealed class TileOcclusionNeighbourTestTile : TileBase
{
    public Sprite Sprite;
    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = Sprite;
        tileData.color = tilemap.GetTile(position + Vector3Int.left) != null ? Color.red : Color.blue;
        tileData.transform = Matrix4x4.identity;
        tileData.flags = TileFlags.LockAll;
        tileData.colliderType = Tile.ColliderType.None;
    }
}
