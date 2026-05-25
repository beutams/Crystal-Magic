using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Core
{
    internal sealed class DungeonSceneRuntimeRoot : MonoBehaviour
    {
        private readonly List<Material> _trackedMaterials = new();
        private readonly List<Entity> _spawnedEntities = new();
        private readonly List<Entity> _exitRoomMonsters = new();

        private RuntimeDungeonObjectData _nextLevelEntranceObject;
        private Renderer _exitRenderer;
        private Material _exitClosedMaterial;
        private Material _exitOpenMaterial;
        private float _interactionRange = 2.5f;
        private string _resourceOwnerKey;
        private int _currentFloor = 1;
        private bool _inputSubscribed;
        private bool _exitOpened;
        private bool _transitionRequested;

        public void Initialize(
            int currentFloor,
            RuntimeDungeonObjectData nextLevelEntranceObject,
            Renderer exitRenderer,
            Material exitClosedMaterial,
            Material exitOpenMaterial,
            float interactionRange,
            string resourceOwnerKey,
            IReadOnlyList<Material> trackedMaterials,
            IReadOnlyList<Entity> spawnedEntities,
            IReadOnlyList<Entity> exitRoomMonsters)
        {
            _currentFloor = Mathf.Max(1, currentFloor);
            _nextLevelEntranceObject = nextLevelEntranceObject;
            _exitRenderer = exitRenderer;
            _exitClosedMaterial = exitClosedMaterial;
            _exitOpenMaterial = exitOpenMaterial;
            _interactionRange = Mathf.Max(0.5f, interactionRange);
            _resourceOwnerKey = resourceOwnerKey;

            _trackedMaterials.Clear();
            _spawnedEntities.Clear();
            _exitRoomMonsters.Clear();

            if (trackedMaterials != null)
            {
                for (int i = 0; i < trackedMaterials.Count; i++)
                {
                    if (trackedMaterials[i] != null)
                        _trackedMaterials.Add(trackedMaterials[i]);
                }
            }

            if (spawnedEntities != null)
            {
                for (int i = 0; i < spawnedEntities.Count; i++)
                    _spawnedEntities.Add(spawnedEntities[i]);
            }

            if (exitRoomMonsters != null)
            {
                for (int i = 0; i < exitRoomMonsters.Count; i++)
                    _exitRoomMonsters.Add(exitRoomMonsters[i]);
            }

            RefreshExitState(force: true);
        }

        private void Update()
        {
            if (!_inputSubscribed && InputComponent.TryGetInstance(out InputComponent inputComponent))
            {
                inputComponent.OnInteract += HandleInteract;
                _inputSubscribed = true;
            }

            RefreshExitState(force: false);
        }

        private void OnDestroy()
        {
            if (_inputSubscribed && InputComponent.TryGetInstance(out InputComponent inputComponent))
            {
                inputComponent.OnInteract -= HandleInteract;
            }

            DestroyTrackedEntities();
            for (int i = 0; i < _trackedMaterials.Count; i++)
                DestroyTrackedMaterial(_trackedMaterials[i]);
            _trackedMaterials.Clear();
            ResourceComponent.Instance?.ReleaseOwner(_resourceOwnerKey);
        }

        private void HandleInteract()
        {
            if (!_exitOpened || _transitionRequested || _nextLevelEntranceObject == null)
                return;

            if (TransitionComponent.Instance != null && TransitionComponent.Instance.IsTransitioning)
                return;

            if (!TryGetPlayerPosition(out Vector3 playerPosition))
                return;

            Vector2 offset = (Vector2)(playerPosition - _nextLevelEntranceObject.WorldPosition);
            if (offset.sqrMagnitude > _interactionRange * _interactionRange)
                return;

            LoadGameContext context = SaveDataComponent.Instance?.CreateLoadGameContext(
                SaveAreaType.Dungeon,
                _currentFloor + 1);
            if (context == null || GameFlowComponent.Instance == null)
                return;

            _transitionRequested = true;
            GameFlowComponent.Instance.BeginTransition(DungeonState.CreateEnterTransitionData(context));
        }

        private void RefreshExitState(bool force)
        {
            bool nextOpened = AreExitRoomMonstersCleared();
            if (!force && _exitOpened == nextOpened)
                return;

            _exitOpened = nextOpened;
            if (_exitRenderer != null)
                _exitRenderer.sharedMaterial = _exitOpened ? _exitOpenMaterial : _exitClosedMaterial;
        }

        private bool AreExitRoomMonstersCleared()
        {
            if (_exitRoomMonsters.Count == 0)
                return true;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            EntityManager entityManager = world.EntityManager;
            for (int i = 0; i < _exitRoomMonsters.Count; i++)
            {
                if (IsEntityAlive(entityManager, _exitRoomMonsters[i]))
                    return false;
            }

            return true;
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
            _exitRoomMonsters.Clear();
        }

        private static bool TryGetPlayerPosition(out Vector3 playerPosition)
        {
            playerPosition = default;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            EntityManager entityManager = world.EntityManager;
            EntityQuery playerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalToWorld>());
            if (playerQuery.IsEmptyIgnoreFilter)
                return false;

            LocalToWorld playerTransform = playerQuery.GetSingleton<LocalToWorld>();
            playerPosition = new Vector3(playerTransform.Position.x, playerTransform.Position.y, playerTransform.Position.z);
            return true;
        }

        private static bool IsEntityAlive(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity))
                return false;

            if (!entityManager.HasComponent<DestroyEntityFlag>(entity))
                return true;

            return !entityManager.IsComponentEnabled<DestroyEntityFlag>(entity);
        }

        private static void DestroyTrackedMaterial(Material material)
        {
            if (material == null)
                return;

            UnityEngine.Object.Destroy(material);
        }
    }
}
