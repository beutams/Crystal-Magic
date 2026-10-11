using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.OpenField;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class MinimapUIModel : UIModelBase
    {
        public const string DataChangedEventName = "MinimapUIModel.DataChanged";

        private static readonly Color32 BoundaryColor = new(18, 24, 30, 255);
        private static readonly Color32 GroundColor = new(63, 105, 70, 255);
        private static readonly Color32 VoidColor = new(24, 20, 30, 255);
        private static readonly Color32 ObstacleColor = new(126, 105, 80, 255);
        private OpenFieldDungeonLayout _layout;
        private Texture2D _terrainTexture;
        private Sprite _terrainSprite;
        private Entity _cachedPlayerEntity = Entity.Null;
        private Vector2 _worldOrigin;
        private float _cellWorldSize = 1f;
        private int _textureWidth;
        private int _textureHeight;
        private readonly HashSet<int> _interestPointIds = new();
        private readonly HashSet<int> _clearedInterestPointIds = new();
        private World _interestPointQueryWorld;
        private EntityQuery _interestPointQuery;
        private bool _hasPlayer;
        private Vector2 _playerPosition;
        private float _playerRotationDegrees;
        private World _markerQueryWorld;
        private EntityQuery _teammateQuery, _revengeQuery, _revengeMapQuery;
        private readonly List<MinimapUnitMarker> _teammates = new(), _nextTeammates = new();
        private readonly List<MinimapUnitMarker> _revengeSquads = new(), _nextRevengeSquads = new();

        public override string ChangedEventName => DataChangedEventName;
        public bool HasMap => _terrainSprite != null;
        public Sprite TerrainSprite => _terrainSprite;
        public OpenFieldDungeonLayout Layout => _layout;
        public bool HasPlayer => _hasPlayer;
        public Vector2 PlayerPosition => _playerPosition;
        public float PlayerRotationDegrees => _playerRotationDegrees;
        public IReadOnlyList<MinimapUnitMarker> Teammates => _teammates;
        public IReadOnlyList<MinimapUnitMarker> RevengeSquads => _revengeSquads;

        public void Refresh()
        {
            bool changed = EnsureMap();
            changed |= RefreshPlayerMarker();
            changed |= RefreshInterestPointStates();
            changed |= RefreshUnitMarkers();
            if (changed)
                PublishChanged();
        }

        public void RefreshRuntime()
        {
            Refresh();
        }

        public override void Dispose()
        {
            ReleaseMapVisual();
            _layout = null;
            _cachedPlayerEntity = Entity.Null;
            base.Dispose();
        }

        private bool EnsureMap()
        {
            DungeonRuntimeMapComponent mapData = GameRuntimeStateUtility.GetDungeonRuntimeMap();
            if (mapData == null)
                return false;
            OpenFieldDungeonLayout layout = mapData.OpenFieldLayout;
            if (layout == null || layout.Width <= 0 || layout.Height <= 0)
            {
                if (_layout == null && _terrainSprite == null)
                    return false;

                ReleaseMapVisual();
                _layout = null;
                _hasPlayer = false;
                _cachedPlayerEntity = Entity.Null;
                return true;
            }

            if (ReferenceEquals(_layout, layout) && _terrainSprite != null)
                return false;

            ReleaseMapVisual();
            _layout = layout;
            foreach (OpenFieldInterestPoint point in layout.InterestPoints)
                _interestPointIds.Add(point.EncounterId);
            _cachedPlayerEntity = Entity.Null;
            ConfigureWorldSpace(mapData.SceneData, layout);
            BuildTerrainSprite(layout);
            _hasPlayer = false;
            return true;
        }

        private void ConfigureWorldSpace(RuntimeDungeonSceneData sceneData, OpenFieldDungeonLayout layout)
        {
            _cellWorldSize = sceneData != null && sceneData.CellWorldSize > 0f
                ? sceneData.CellWorldSize
                : 1f;
            _worldOrigin = sceneData?.TerrainVisual != null
                ? sceneData.TerrainVisual.WorldOrigin
                : new Vector2(-layout.Width * _cellWorldSize * 0.5f, -layout.Height * _cellWorldSize * 0.5f);
        }

        private void BuildTerrainSprite(OpenFieldDungeonLayout layout)
        {
            _textureWidth = layout.Width + 2;
            _textureHeight = layout.Height + 2;
            Color32[] pixels = new Color32[_textureWidth * _textureHeight];
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = BoundaryColor;

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                    pixels[(x + 1) + (y + 1) * _textureWidth] = GetTerrainColor(layout.GetTerrainCell(x, y));
            }

            _terrainTexture = new Texture2D(_textureWidth, _textureHeight, TextureFormat.RGBA32, false)
            {
                name = "RuntimeDungeonMinimap",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            _terrainTexture.SetPixels32(pixels);
            _terrainTexture.Apply(false, false);
            _terrainSprite = Sprite.Create(
                _terrainTexture,
                new Rect(0f, 0f, _textureWidth, _textureHeight),
                new Vector2(0.5f, 0.5f),
                1f);
            _terrainSprite.name = "RuntimeDungeonMinimapSprite";
            _terrainSprite.hideFlags = HideFlags.DontSave;
        }

        private bool RefreshPlayerMarker()
        {
            if (!HasMap)
                return false;

            if (!TryGetPlayerPose(out Vector3 position, out float rotationDegrees))
            {
                if (!_hasPlayer)
                    return false;

                _hasPlayer = false;
                return true;
            }

            Vector2 nextPosition = ToTextureNormalized(new Vector2(
                (position.x - _worldOrigin.x) / _cellWorldSize,
                (position.y - _worldOrigin.y) / _cellWorldSize));
            bool changed = !_hasPlayer ||
                           (nextPosition - _playerPosition).sqrMagnitude > 0.000001f ||
                           Mathf.Abs(Mathf.DeltaAngle(_playerRotationDegrees, rotationDegrees)) > 0.1f;
            if (!changed)
                return false;

            _hasPlayer = true;
            _playerPosition = nextPosition;
            _playerRotationDegrees = rotationDegrees;
            return true;
        }

        private bool TryGetPlayerPose(out Vector3 position, out float rotationDegrees)
        {
            position = Vector3.zero;
            rotationDegrees = _playerRotationDegrees;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            EntityManager entityManager = world.EntityManager;
            if (!TryGetPlayerEntity(entityManager, out Entity player))
                return false;

            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(player);
            position = new Vector3(transform.Position.x, transform.Position.y, transform.Position.z);
            if (!entityManager.HasComponent<UnitFacingComponent>(player))
                return true;

            float2 direction = entityManager.GetComponentData<UnitFacingComponent>(player).Direction;
            if (math.lengthsq(direction) > 0.0001f)
                // The existing BookV1 triangle sprite points down at zero rotation.
                rotationDegrees = math.degrees(math.atan2(direction.y, direction.x)) + 90f;
            return true;
        }

        private bool TryGetPlayerEntity(EntityManager entityManager, out Entity player)
        {
            if (_cachedPlayerEntity != Entity.Null &&
                entityManager.Exists(_cachedPlayerEntity) &&
                entityManager.HasComponent<UnitFactionComponent>(_cachedPlayerEntity) &&
                entityManager.HasComponent<LocalTransform>(_cachedPlayerEntity) &&
                (GameWorldManager.Role != GameWorldRole.Client ||
                 entityManager.HasComponent<NetworkPlayerComponent>(_cachedPlayerEntity)) &&
                UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(_cachedPlayerEntity).Value))
            {
                player = _cachedPlayerEntity;
                return true;
            }

            if (GameRuntimeStateUtility.TryGetPlayerEntity(entityManager, out Entity entity) &&
                entityManager.HasComponent<UnitFactionComponent>(entity) &&
                entityManager.HasComponent<LocalTransform>(entity) &&
                UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(entity).Value))
            {
                _cachedPlayerEntity = entity;
                player = entity;
                return true;
            }

            player = Entity.Null;
            return false;
        }

        private Vector2 ToTextureNormalized(Vector2 mapCellPosition)
        {
            return new Vector2(
                Mathf.Clamp((mapCellPosition.x + 1f) / _textureWidth, 1f / _textureWidth, 1f - 1f / _textureWidth),
                Mathf.Clamp((mapCellPosition.y + 1f) / _textureHeight, 1f / _textureHeight, 1f - 1f / _textureHeight));
        }

        private Vector2 WorldToNormalized(float3 position) => ToTextureNormalized(new Vector2(
            (position.x - _worldOrigin.x) / _cellWorldSize, (position.y - _worldOrigin.y) / _cellWorldSize));

        private bool RefreshUnitMarkers()
        {
            _nextTeammates.Clear();
            _nextRevengeSquads.Clear();
            World world = World.DefaultGameObjectInjectionWorld;
            if (HasMap && world != null && world.IsCreated)
            {
                EntityManager manager = world.EntityManager;
                if (_markerQueryWorld != world)
                {
                    ReleaseMarkerQueries();
                    _markerQueryWorld = world;
                    _teammateQuery = manager.CreateEntityQuery(new EntityQueryDesc
                    {
                        All = new[] { ComponentType.ReadOnly<PlayerCharacterComponent>(),
                            ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<NetworkEntitySpawnInfoComponent>() },
                        None = new[] { ComponentType.ReadOnly<NetworkPlayerComponent>(), ComponentType.ReadOnly<DestroyEntityFlag>() },
                    });
                    _revengeQuery = manager.CreateEntityQuery(new EntityQueryDesc
                    {
                        All = new[] { ComponentType.ReadOnly<DungeonRevengeSquadMember>(), ComponentType.ReadOnly<LocalTransform>() },
                        None = new[] { ComponentType.ReadOnly<UnitDeathComponent>(), ComponentType.ReadOnly<DestroyEntityFlag>() },
                        Options = EntityQueryOptions.IncludeDisabledEntities,
                    });
                    _revengeMapQuery = manager.CreateEntityQuery(ComponentType.ReadOnly<DungeonRevengeSquadMapElement>());
                }
                using NativeArray<Entity> teammates = _teammateQuery.ToEntityArray(Allocator.Temp);
                foreach (Entity entity in teammates)
                {
                    if (entity == _cachedPlayerEntity ||
                        manager.GetComponentObject<NetworkEntitySpawnInfoComponent>(entity).entityInfo?.ownerAccountId is null or 0UL)
                        continue;
                    float rotation = 0;
                    if (manager.HasComponent<UnitFacingComponent>(entity))
                    {
                        float2 direction = manager.GetComponentData<UnitFacingComponent>(entity).Direction;
                        rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
                    }
                    _nextTeammates.Add(new MinimapUnitMarker { Id = entity.Index,
                        Position = WorldToNormalized(manager.GetComponentData<LocalTransform>(entity).Position), RotationDegrees = rotation });
                }
                _nextTeammates.Sort((a, b) => a.Id.CompareTo(b.Id));
                using NativeArray<Entity> maps = _revengeMapQuery.ToEntityArray(Allocator.Temp);
                if (maps.Length > 0)
                {
                    foreach (DungeonRevengeSquadMapElement point in manager.GetBuffer<DungeonRevengeSquadMapElement>(maps[0], true))
                        _nextRevengeSquads.Add(new MinimapUnitMarker { Id = point.SquadId, Position = WorldToNormalized(point.Position) });
                }
                else
                {
                    using NativeArray<Entity> members = _revengeQuery.ToEntityArray(Allocator.Temp);
                    NativeHashMap<int, float4> centers = new(math.max(1, members.Length), Allocator.Temp);
                    foreach (Entity entity in members)
                    {
                        int id = manager.GetComponentData<DungeonRevengeSquadMember>(entity).SquadId;
                        centers.TryGetValue(id, out float4 center);
                        centers[id] = center + new float4(manager.GetComponentData<LocalTransform>(entity).Position, 1f);
                    }
                    foreach (var point in centers)
                        _nextRevengeSquads.Add(new MinimapUnitMarker { Id = point.Key, Position = WorldToNormalized(point.Value.xyz / point.Value.w) });
                    centers.Dispose();
                }
                _nextRevengeSquads.Sort((a, b) => a.Id.CompareTo(b.Id));
            }
            return ReplaceMarkers(_teammates, _nextTeammates) | ReplaceMarkers(_revengeSquads, _nextRevengeSquads);
        }

        private static bool ReplaceMarkers(List<MinimapUnitMarker> current, List<MinimapUnitMarker> next)
        {
            bool changed = current.Count != next.Count;
            for (int i = 0; !changed && i < current.Count; i++)
                changed |= current[i].Id != next[i].Id || current[i].Position != next[i].Position ||
                           Mathf.Abs(Mathf.DeltaAngle(current[i].RotationDegrees, next[i].RotationDegrees)) > 0.1f;
            if (changed) { current.Clear(); current.AddRange(next); }
            return changed;
        }

        private void ReleaseMarkerQueries()
        {
            if (_markerQueryWorld != null && _markerQueryWorld.IsCreated)
            {
                _teammateQuery.Dispose(); _revengeQuery.Dispose(); _revengeMapQuery.Dispose();
            }
            _markerQueryWorld = null;
            _teammateQuery = _revengeQuery = _revengeMapQuery = default;
        }

        public Vector2 GetInterestPointPosition(OpenFieldInterestPoint point)
        {
            return ToTextureNormalized(new Vector2(point.Center.X + 0.5f, point.Center.Y + 0.5f));
        }

        public bool IsInterestPointCleared(int encounterId)
        {
            return _clearedInterestPointIds.Contains(encounterId);
        }

        private bool RefreshInterestPointStates()
        {
            if (!HasMap)
                return false;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            if (_interestPointQueryWorld != world)
            {
                ReleaseInterestPointQuery();
                _interestPointQueryWorld = world;
                _interestPointQuery = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<DungeonInterestPointComponent>());
            }

            bool changed = false;
            EntityManager entityManager = world.EntityManager;
            using NativeArray<Entity> points = _interestPointQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity entity in points)
            {
                int id = entityManager.GetComponentData<DungeonInterestPointComponent>(entity).EncounterId;
                // Use the encounter's authoritative terminal state, not its current guard count.
                if (_interestPointIds.Contains(id) && DungeonPatrolRuntimeUtility.IsEncounterDead(entityManager, entity))
                    changed |= _clearedInterestPointIds.Add(id);
            }

            return changed;
        }

        private void ReleaseInterestPointQuery()
        {
            if (_interestPointQueryWorld != null && _interestPointQueryWorld.IsCreated)
                _interestPointQuery.Dispose();
            _interestPointQueryWorld = null;
            _interestPointQuery = default;
        }

        private void ReleaseMapVisual()
        {
            ReleaseInterestPointQuery();
            ReleaseMarkerQueries();
            _teammates.Clear();
            _revengeSquads.Clear();
            _interestPointIds.Clear();
            _clearedInterestPointIds.Clear();
            if (_terrainSprite != null)
                Object.Destroy(_terrainSprite);
            if (_terrainTexture != null)
                Object.Destroy(_terrainTexture);

            _terrainSprite = null;
            _terrainTexture = null;
            _textureWidth = 0;
            _textureHeight = 0;
        }

        private static Color32 GetTerrainColor(OpenFieldTerrainCell terrain)
        {
            return terrain switch
            {
                OpenFieldTerrainCell.Ground => GroundColor,
                OpenFieldTerrainCell.Void => VoidColor,
                OpenFieldTerrainCell.Obstacle => ObstacleColor,
                _ => BoundaryColor,
            };
        }

        private void PublishChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }
    }

    public struct MinimapUnitMarker
    {
        public int Id;
        public Vector2 Position;
        public float RotationDegrees;
    }
}
