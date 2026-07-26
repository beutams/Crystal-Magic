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

        private void OnDestroy()
        {
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
