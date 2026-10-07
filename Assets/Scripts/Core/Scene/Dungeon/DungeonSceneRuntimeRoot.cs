using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.Core
{
    internal sealed class DungeonSceneRuntimeRoot : MonoBehaviour
    {
        private List<Entity> _spawnedEntities;
        private readonly List<Object> _runtimeAssets = new();
        private string _resourceOwnerKey;
        private bool _hasCameraWorldBounds;
        private World _owningWorld;
        private bool _released;

        public void Initialize(string resourceOwnerKey, List<Entity> spawnedEntities, World owningWorld)
        {
            _released = false;
            _owningWorld = owningWorld;
            _resourceOwnerKey = resourceOwnerKey;
            // Track partial builds before the first yield, not only on success.
            _spawnedEntities = spawnedEntities;
        }

        public void BindWorld(World world) => _owningWorld = world;

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
            // Never query world-wide tags from delayed OnDestroy: a replacement
            // dungeon may already have been built in this World.
            ReleaseContents(false);
        }

        public void ReleaseContents(bool includeRuntimeDescendants = true)
        {
            if (_released) return;
            _released = true;
            if (_hasCameraWorldBounds)
                CameraComponent.Instance?.ClearWorldBounds(GetInstanceID());
            _hasCameraWorldBounds = false;

            try { DestroyTrackedEntities(includeRuntimeDescendants); }
            finally
            {
                DestroyRuntimeAssets();
                ResourceComponent.Instance?.ReleaseOwner(_resourceOwnerKey);
                _resourceOwnerKey = null;
                _owningWorld = null;
                _spawnedEntities?.Clear();
            }
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

        private void DestroyTrackedEntities(bool includeRuntimeDescendants)
        {
            World world = _owningWorld;
            if (world == null || !world.IsCreated)
                return;

            EntityManager entityManager = world.EntityManager;
            entityManager.CompleteAllTrackedJobs();
            for (int i = 0; _spawnedEntities != null && i < _spawnedEntities.Count; i++)
            {
                Entity entity = _spawnedEntities[i];
                if (entity != Entity.Null && entityManager.Exists(entity))
                    entityManager.DestroyEntity(entity);
            }

            if (!includeRuntimeDescendants) return;

            using EntityQuery runtimeOwnedQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<DungeonRuntimeOwnedEntity>() },
                Options = EntityQueryOptions.IncludeDisabledEntities,
            });
            if (!runtimeOwnedQuery.IsEmptyIgnoreFilter)
                entityManager.DestroyEntity(runtimeOwnedQuery);
        }
    }
}
