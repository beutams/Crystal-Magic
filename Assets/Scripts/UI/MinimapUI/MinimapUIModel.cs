using CrystalMagic.Core;
using CrystalMagic.Game.OpenField;
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
        private bool _hasExit;
        private Vector2 _exitPosition;
        private bool _hasPlayer;
        private Vector2 _playerPosition;
        private float _playerRotationDegrees;

        public override string ChangedEventName => DataChangedEventName;
        public bool HasMap => _terrainSprite != null;
        public Sprite TerrainSprite => _terrainSprite;
        public OpenFieldDungeonLayout Layout => _layout;
        public bool HasExit => _hasExit;
        public Vector2 ExitPosition => _exitPosition;
        public bool HasPlayer => _hasPlayer;
        public Vector2 PlayerPosition => _playerPosition;
        public float PlayerRotationDegrees => _playerRotationDegrees;

        public void Refresh()
        {
            bool changed = EnsureMap();
            changed |= RefreshPlayerMarker();
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
            RuntimeDungeonMapData mapData = RuntimeDataComponent.Instance.GetDungeonMapData();
            OpenFieldDungeonLayout layout = mapData.OpenFieldLayout;
            if (layout == null || layout.Width <= 0 || layout.Height <= 0)
            {
                if (_layout == null && _terrainSprite == null)
                    return false;

                ReleaseMapVisual();
                _layout = null;
                _hasExit = false;
                _hasPlayer = false;
                _cachedPlayerEntity = Entity.Null;
                return true;
            }

            if (ReferenceEquals(_layout, layout) && _terrainSprite != null)
                return false;

            ReleaseMapVisual();
            _layout = layout;
            _cachedPlayerEntity = Entity.Null;
            ConfigureWorldSpace(mapData.SceneData, layout);
            BuildTerrainSprite(layout);
            ConfigureExitMarker(layout);
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

        private void ConfigureExitMarker(OpenFieldDungeonLayout layout)
        {
            OpenFieldInterestPoint exitPoint = layout.ExitInterestPoint;
            _hasExit = exitPoint != null;
            _exitPosition = _hasExit
                ? ToTextureNormalized(new Vector2(exitPoint.Center.X + 0.5f, exitPoint.Center.Y + 0.5f))
                : Vector2.zero;
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
            rotationDegrees = 0f;

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
                rotationDegrees = math.degrees(math.atan2(direction.y, direction.x)) - 90f;
            return true;
        }

        private bool TryGetPlayerEntity(EntityManager entityManager, out Entity player)
        {
            if (_cachedPlayerEntity != Entity.Null &&
                entityManager.Exists(_cachedPlayerEntity) &&
                entityManager.HasComponent<UnitFactionComponent>(_cachedPlayerEntity) &&
                entityManager.HasComponent<LocalTransform>(_cachedPlayerEntity) &&
                UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(_cachedPlayerEntity).Value))
            {
                player = _cachedPlayerEntity;
                return true;
            }

            EntityQuery query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitFactionComponent>(),
                ComponentType.ReadOnly<LocalTransform>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities[index];
                if (!UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(entity).Value))
                    continue;

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

        public void GetInterestPointAnchorRange(
            OpenFieldInterestPoint point,
            out Vector2 anchorMin,
            out Vector2 anchorMax)
        {
            int radius = Mathf.Max(1, point.Radius);
            anchorMin = new Vector2(
                Mathf.Clamp01((point.Center.X - radius + 1f) / _textureWidth),
                Mathf.Clamp01((point.Center.Y - radius + 1f) / _textureHeight));
            anchorMax = new Vector2(
                Mathf.Clamp01((point.Center.X + radius + 2f) / _textureWidth),
                Mathf.Clamp01((point.Center.Y + radius + 2f) / _textureHeight));
        }

        private void ReleaseMapVisual()
        {
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
}
