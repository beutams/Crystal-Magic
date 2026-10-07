using System;
using System.Linq;
using CrystalMagic.Game.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace CrystalMagic.Editor.Map
{
    public static class TileOcclusionExample
    {
        private const string Atlas = "Assets/Res/Sprites/Village/Village_Tileset.png";
        private const string Wizard = "Assets/Res/Sprites/Units/Characters(100x100 split)/Wizard/Wizard/Wizard_Idle.png";

        [MenuItem("Tools/Map/Create Tile Occlusion Example")]
        public static void OpenExample()
        {
            try
            {
                GameObject prefab = Create();
                AssetDatabase.OpenAsset(prefab);
                TileOcclusionWindow.Open();
            }
            catch (Exception exception) { Debug.LogError("[TileOcclusion] " + exception.Message); }
        }

        // Build in an isolated preview scene; never touch the user's open map.
        public static GameObject Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出 Play Mode 后创建样例。");
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToArray();
            Sprite[] tree = Enumerable.Range(1, 9).Select(i => sprites.Single(s => s.name == $"tree1_{i}")).ToArray();
            Sprite grass = sprites.First(s => s.rect.x == 64 && s.rect.y == 1984 && s.rect.width == 64);
            Sprite wizard = AssetDatabase.LoadAllAssetsAtPath(Wizard).OfType<Sprite>().First(s => s.name == "Wizard_Idle_0");
            const string parent = "Assets/Res/Tile/OcclusionExamples";
            TileOcclusionBuilder.EnsureAssetFolder(parent);
            string folder = AssetDatabase.GenerateUniqueAssetPath(parent + "/Example");
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder))))
                throw new InvalidOperationException("无法创建样例目录。");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            int previewUndoGroup = Undo.GetCurrentGroup();
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("TileOcclusionExample");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var mapObject = Child("Map_SelectThisInOcclusionEditor", root.transform);
                mapObject.AddComponent<Grid>();
                var map = mapObject.AddComponent<TileOcclusionMap>();
                var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, folder + "/Example.mat");
                var groundTile = ScriptableObject.CreateInstance<Tile>();
                groundTile.sprite = grass;
                groundTile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(groundTile, folder + "/SourceTiles.asset");
                Tilemap ground = Layer("Ground", map.transform, material, 0);
                Tilemap trees = Layer("Trees", map.transform, material, 1);
                for (int y = -2; y < 5; y++)
                    for (int x = -1; x < 9; x++) ground.SetTile(new Vector3Int(x, y), groundTile);
                Tile[] treeTiles = tree.Select(sprite =>
                {
                    var tile = ScriptableObject.CreateInstance<Tile>();
                    tile.name = sprite.name;
                    tile.sprite = sprite;
                    tile.colliderType = Tile.ColliderType.None;
                    AssetDatabase.AddObjectToAsset(tile, groundTile);
                    return tile;
                }).ToArray();
                for (int copy = 0; copy < 2; copy++)
                {
                    var region = new TileOcclusionRegion { Name = "Tree_" + copy, LocalAnchor = new Vector3(1.5f + copy * 5, 0.4f) };
                    for (int i = 0; i < tree.Length; i++)
                    {
                        var cell = new Vector3Int(Mathf.RoundToInt(tree[i].rect.x / 64) + copy * 5,
                            Mathf.RoundToInt((tree[i].rect.y - 704) / 64));
                        trees.SetTile(cell, treeTiles[i]);
                        region.Cells.Add(new TileOcclusionCell(trees, cell));
                    }
                    map.Regions.Add(region);
                    var collider = Child("TrunkCollision_" + copy, map.transform);
                    collider.transform.localPosition = region.LocalAnchor + Vector3.up * 0.2f;
                    collider.AddComponent<BoxCollider2D>().size = new Vector2(0.4f, 0.4f);
                }
                AssetDatabase.SaveAssetIfDirty(groundTile);
                TileOcclusionBuilder.SyncLayers(map);
                map.Layers.Single(l => l.Source == trees).Selectable = true;
                TileOcclusionBuilder.Generate(map, folder + "/Snapshots");
                Actor("Front_DragThisAnchor", new Vector3(1.5f, 0.15f), root.transform, wizard, material);
                Actor("Behind_DragThisAnchor", new Vector3(5.45f, 0.95f), root.transform, wizard, material);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/TileOcclusionExample.prefab");
                if (prefab == null) throw new InvalidOperationException("样例 Prefab 保存失败。");
                Debug.Log("遮挡样例：" + AssetDatabase.GetAssetPath(prefab) + "。左边角色在树前，右边在树后；沿 Y 拖动两个 DragThisAnchor 节点可看排序变化。仅用于展示，不含单位逻辑。");
                return prefab;
            }
            finally
            {
                // Do not leave dead preview-scene objects in the user's Undo stack.
                // The separately saved prefab/assets remain intact.
                Undo.RevertAllDownToGroup(previewUndoGroup);
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Actor(string name, Vector3 position, Transform parent, Sprite sprite, Material material)
        {
            GameObject anchor = Child(name, parent);
            anchor.transform.localPosition = position;
            var actor = Child("Visual", anchor.transform);
            const float scale = 6.25f;
            actor.transform.localScale = Vector3.one * scale;
            // The existing 100px frame has padding; align its visible feet to the
            // anchor without changing the source sprite import settings.
            actor.transform.localPosition = (sprite.pivot / sprite.pixelsPerUnit - new Vector2(0.51f, 0.43f)) * scale;
            var renderer = actor.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            anchor.AddComponent<TileOcclusionSortAnchor>().Refresh();
        }

        private static Tilemap Layer(string name, Transform parent, Material material, int order)
        {
            GameObject obj = Child(name, parent);
            Tilemap tilemap = obj.AddComponent<Tilemap>();
            var renderer = obj.AddComponent<TilemapRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            return tilemap;
        }

        private static GameObject Child(string name, Transform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj;
        }
    }
}
