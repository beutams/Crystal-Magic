using UnityEngine;
using CrystalMagic.Game.Map;

namespace CrystalMagic.Core
{
    // The editor preview and runtime loader must use the same asset and transform.
    public readonly struct SceneMapLayout
    {
        public static SceneMapLayout Town => new(
            "Assets/Res/Tile/OcclusionMaps/TownMap/TownMap_Occlusion.prefab", Vector3.one);
        public static SceneMapLayout Training => new(
            "Assets/Res/Tile/OcclusionMaps/TrainingMap/TrainingMap_Occlusion.prefab", Vector3.one);

        public string PrefabPath { get; }
        public Vector3 Scale { get; }

        private SceneMapLayout(string prefabPath, Vector3 scale)
        {
            PrefabPath = prefabPath;
            Scale = scale;
        }

        public void Apply(Transform root)
        {
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.localScale = Scale;
        }

        public static Bounds GetCameraWorldBounds(GameObject root)
        {
            var map = root.GetComponent<TileOcclusionMap>();
            if (map == null || map.CollisionData == null)
                throw new System.InvalidOperationException("Map bounds require a collision grid.");
            var grid = TileCollisionNavigationAuthoring.Describe(map.CollisionData, root.transform);
            var bounds = new Bounds(new Vector3(grid.WorldOrigin.x + grid.Width * grid.CellSize * 0.5f,
                grid.WorldOrigin.y + grid.Height * grid.CellSize * 0.5f, 0),
                new Vector3(grid.Width * grid.CellSize, grid.Height * grid.CellSize, 0));
            float minZ = 0, maxZ = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                minZ = Mathf.Min(minZ, renderer.bounds.min.z);
                maxZ = Mathf.Max(maxZ, renderer.bounds.max.z);
            }
            // Imported TMX layers have different Z offsets. Perspective views
            // must fit the furthest floor too, not just the Z=0 collision plane.
            bounds.SetMinMax(new Vector3(bounds.min.x, bounds.min.y, minZ),
                new Vector3(bounds.max.x, bounds.max.y, maxZ));
            return bounds;
        }
    }
}
