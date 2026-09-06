using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.Core
{
    internal sealed class DungeonSceneRuntimeRoot : MonoBehaviour
    {
        private readonly List<Entity> _spawnedEntities = new();
        private readonly List<Object> _runtimeAssets = new();
        private string _resourceOwnerKey;
        private bool _hasCameraWorldBounds;

        public void Initialize(string resourceOwnerKey, IReadOnlyList<Entity> spawnedEntities)
        {
            _resourceOwnerKey = resourceOwnerKey;
            _spawnedEntities.Clear();

            if (spawnedEntities == null)
                return;

            for (int i = 0; i < spawnedEntities.Count; i++)
                _spawnedEntities.Add(spawnedEntities[i]);
        }

        public void TrackRuntimeAsset(Object runtimeAsset)
        {
            if (runtimeAsset != null)
                _runtimeAssets.Add(runtimeAsset);
        }

        public void TrackRuntimeAssets(params Object[] runtimeAssets)
        {
            if (runtimeAssets == null)
                return;

            for (int i = 0; i < runtimeAssets.Length; i++)
                TrackRuntimeAsset(runtimeAssets[i]);
        }

        public void SetCameraWorldBounds(Rect worldBounds)
        {
            _hasCameraWorldBounds = worldBounds.width > 0f && worldBounds.height > 0f;
            if (_hasCameraWorldBounds)
                CameraComponent.Instance?.SetWorldBounds(GetInstanceID(), worldBounds);
            else
                CameraComponent.Instance?.ClearWorldBounds(GetInstanceID());
        }

        private void OnDestroy()
        {
            if (_hasCameraWorldBounds)
                CameraComponent.Instance?.ClearWorldBounds(GetInstanceID());

            DestroyTrackedEntities();
            DestroyRuntimeAssets();
            ResourceComponent.Instance?.ReleaseOwner(_resourceOwnerKey);
        }

        private void DestroyRuntimeAssets()
        {
            for (int i = 0; i < _runtimeAssets.Count; i++)
            {
                if (_runtimeAssets[i] != null)
                    Destroy(_runtimeAssets[i]);
            }

            _runtimeAssets.Clear();
        }

        private void DestroyTrackedEntities()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            EntityManager entityManager = world.EntityManager;
            for (int i = 0; i < _spawnedEntities.Count; i++)
            {
                Entity entity = _spawnedEntities[i];
                if (entity != Entity.Null && entityManager.Exists(entity))
                    entityManager.DestroyEntity(entity);
            }

            _spawnedEntities.Clear();
        }
    }
}
