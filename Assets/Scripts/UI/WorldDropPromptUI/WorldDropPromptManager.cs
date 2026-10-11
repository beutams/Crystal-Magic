using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Server;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class InteractionPromptManager : System.IDisposable
    {
        private const string GroupName = "Bottom";
        private const float DefaultWorldYOffset = 0.8f;
        private const float CharacterWorldYOffset = 1.2f;
        private const float PlayerFallbackHeadHeight = .15f;
        private const string MoneyDisplayNameKey = "world.drop.money";
        private const string TreasureDisplayNameKey = "world.drop.treasure";

        private RectTransform _rootRect;
        private Camera _currentCamera;
        private InteractionPromptUI _view;
        private InteractionPromptUIModel _model;
        private World _playerWorld;
        private Entity _cachedPlayer;
        private Entity _cachedTarget;
        private UnitInteractionData _cachedInteraction;
        private string _cachedDisplayName;
        private float _cachedWorldYOffset;
        private bool _hasCachedDisplay;
        private bool _initialized;
        private readonly Dictionary<Entity, PlayerNameplate> _playerNameplates = new();
        private readonly List<Entity> _staleNameplates = new();
        private World _nameplateWorld;
        private EntityQuery _nameplateQuery;

        public void Initialize()
        {
            if (_initialized)
                return;

            ResolveFloatingRoot();
            EnsurePromptView();
            SetVisible(false);
            LocalizationComponent.LanguageChanged += HandleLanguageChanged;
            // Project only after camera follow, shake and companion transforms have updated.
            Canvas.preWillRenderCanvases += RefreshWorldLabels;
            _initialized = true;
        }

        public void Tick()
        {
            if (_initialized && _view == null)
            {
                ResolveFloatingRoot();
                EnsurePromptView();
            }
        }

        private void RefreshWorldLabels()
        {
            RefreshPrompt();
            RefreshPlayerNameplates();
        }

        private void RefreshPrompt()
        {
            if (!_initialized)
                return;

            // A live scene may replace its camera without destroying the previous one.
            _currentCamera = CameraComponent.Instance.Current;
            if (_rootRect == null || _currentCamera == null || _model == null)
            {
                SetVisible(false);
                return;
            }

            if (!TryGetPromptTarget(out float3 worldPosition, out string displayName, out float worldYOffset))
            {
                SetVisible(false);
                return;
            }

            Vector3 screenPosition = _currentCamera.WorldToScreenPoint((Vector3)worldPosition + Vector3.up * worldYOffset);
            if (screenPosition.z <= 0f)
            {
                SetVisible(false);
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, screenPosition, null, out Vector2 localPoint))
            {
                SetVisible(false);
                return;
            }

            _model.SetDisplay(displayName, localPoint, true);
        }

        public void Dispose()
        {
            LocalizationComponent.LanguageChanged -= HandleLanguageChanged;
            Canvas.preWillRenderCanvases -= RefreshWorldLabels;
            ReleasePlayerNameplates();
            _playerWorld = null;
            _cachedPlayer = Entity.Null;

            if (_view != null)
                UIComponent.Instance.ReleaseUI(_view);

            _view = null;
            _model = null;
            _rootRect = null;
            _currentCamera = null;
            InvalidateCachedDisplay();
            _initialized = false;
        }

        private void RefreshPlayerNameplates()
        {
            if (!_initialized)
                return;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated ||
                (world.Flags & WorldFlags.GameClient) != WorldFlags.GameClient)
            {
                ReleasePlayerNameplates();
                return;
            }

            ClientBattleManager battle = NetworkComponent.Instance.clientBattleManager;
            if (battle.localAccountId == 0UL)
            {
                ReleasePlayerNameplates();
                return;
            }

            if (_nameplateWorld != world)
            {
                ReleasePlayerNameplates();
                _nameplateWorld = world;
                _nameplateQuery = world.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<NetworkEntitySpawnInfoComponent>(),
                        ComponentType.ReadOnly<PlayerCharacterComponent>(),
                        ComponentType.ReadOnly<LocalToWorld>(),
                    },
                    None = new[] { ComponentType.ReadOnly<NetworkPlayerComponent>() },
                });
            }

            if (_rootRect == null || _currentCamera == null)
            {
                foreach (PlayerNameplate nameplate in _playerNameplates.Values)
                    nameplate.Model.SetVisible(false);
                return;
            }

            EntityManager entityManager = world.EntityManager;
            foreach (PlayerNameplate nameplate in _playerNameplates.Values)
                nameplate.Seen = false;

            using NativeArray<Entity> players = _nameplateQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity player in players)
            {
                NetworkEntitySpawnInfo info = entityManager.GetComponentObject<NetworkEntitySpawnInfoComponent>(player)?.entityInfo;
                if (info == null || info.prefabType != NetworkEntityPrefabType.Unit ||
                    info.ownerAccountId == 0UL || info.ownerAccountId == battle.localAccountId ||
                    (entityManager.HasComponent<DestroyEntityFlag>(player) &&
                     entityManager.IsComponentEnabled<DestroyEntityFlag>(player)) ||
                    !battle.TryGetPlayerDisplayName(info.ownerAccountId, out string displayName))
                    continue;

                if (!_playerNameplates.TryGetValue(player, out PlayerNameplate nameplate))
                {
                    InteractionPromptUI view = UIComponent.Instance.Open<InteractionPromptUI>();
                    if (view == null)
                        continue;
                    UIComponent.Instance.SetLifetime(view, UILifetime.Manual);
                    nameplate = new PlayerNameplate
                    {
                        View = view,
                        Model = UIComponent.Instance.GetModel<InteractionPromptUIModel>(view),
                    };
                    _playerNameplates.Add(player, nameplate);
                }

                nameplate.Seen = true;
                LocalToWorld localToWorld = entityManager.GetComponentData<LocalToWorld>(player);
                SpriteRenderer renderer = entityManager.HasComponent<SpriteRenderer>(player)
                    ? entityManager.GetComponentObject<SpriteRenderer>(player) : null;
                Vector3 anchor = ResolveNameplateAnchor(nameplate, renderer, localToWorld);
                Vector3 screenPosition = _currentCamera.WorldToScreenPoint(anchor);
                bool visible = screenPosition.z > 0f &&
                    screenPosition.x >= 0f && screenPosition.x <= Screen.width &&
                    screenPosition.y >= 0f && screenPosition.y <= Screen.height;
                if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _rootRect, screenPosition, null, out Vector2 localPoint))
                    nameplate.Model.SetDisplay(displayName, localPoint, true, aboveAnchor: true);
                else
                    nameplate.Model.SetVisible(false);
            }

            _staleNameplates.Clear();
            foreach (KeyValuePair<Entity, PlayerNameplate> pair in _playerNameplates)
                if (!pair.Value.Seen)
                    _staleNameplates.Add(pair.Key);
            foreach (Entity player in _staleNameplates)
            {
                ReleaseNameplate(_playerNameplates[player]);
                _playerNameplates.Remove(player);
            }
        }

        private void ReleasePlayerNameplates()
        {
            foreach (PlayerNameplate nameplate in _playerNameplates.Values)
                ReleaseNameplate(nameplate);
            _playerNameplates.Clear();
            _staleNameplates.Clear();
            if (_nameplateWorld != null && _nameplateWorld.IsCreated)
                _nameplateQuery.Dispose();
            _nameplateWorld = null;
            _nameplateQuery = default;
        }

        private static void ReleaseNameplate(PlayerNameplate nameplate)
        {
            nameplate.Model.SetVisible(false);
            UIComponent.Instance.ReleaseUI(nameplate.View);
        }

        private sealed class PlayerNameplate
        {
            public InteractionPromptUI View;
            public InteractionPromptUIModel Model;
            public bool Seen;
            public bool HasHeadAnchor;
            public float LocalHeadHeight;
        }

        private static Vector3 ResolveNameplateAnchor(PlayerNameplate nameplate, SpriteRenderer renderer, LocalToWorld transform)
        {
            if (!nameplate.HasHeadAnchor && renderer != null && renderer.sprite != null)
            {
                // Player sprites use a tight mesh, which excludes the transparent frame padding.
                // Keep the first visible head height throughout the animation to avoid label bobbing.
                Vector2[] vertices = renderer.sprite.vertices;
                float top = float.NegativeInfinity;
                foreach (Vector2 vertex in vertices) top = Mathf.Max(top, vertex.y);
                nameplate.LocalHeadHeight = vertices.Length > 0 ? top : PlayerFallbackHeadHeight;
                nameplate.HasHeadAnchor = true;
            }

            float height = nameplate.HasHeadAnchor ? nameplate.LocalHeadHeight : PlayerFallbackHeadHeight;
            return renderer != null
                ? renderer.transform.TransformPoint(new Vector3(0, height, 0))
                : math.transform(transform.Value, new float3(0, height, 0));
        }

        private bool ResolveFloatingRoot()
        {
            UIGroup group = UIComponent.Instance.GetGroup<UIGroup>(GroupName);
            if (group == null)
                return false;

            _rootRect = group.transform as RectTransform;
            _currentCamera = CameraComponent.Instance.Current;
            return _rootRect != null && _currentCamera != null;
        }

        private bool EnsurePromptView()
        {
            if (_view != null)
                return true;

            if (_rootRect == null)
                return false;

            _view = UIComponent.Instance.Open<InteractionPromptUI>();
            if (_view == null)
                return false;

            UIComponent.Instance.SetLifetime(_view, UILifetime.Manual);
            _model = UIComponent.Instance.GetModel<InteractionPromptUIModel>(_view);
            return _model != null;
        }

        private bool TryGetPromptTarget(out float3 worldPosition, out string displayName, out float worldYOffset)
        {
            worldPosition = float3.zero;
            displayName = string.Empty;
            worldYOffset = DefaultWorldYOffset;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            EntityManager entityManager = world.EntityManager;
            if (!TryGetPromptPlayer(entityManager, out Entity player))
                return false;
            if (!UnitVariableSource.TryGetValue(
                    entityManager,
                    player,
                    "game.interaction.candidates.0",
                    out UnitSourceValue candidateValue) ||
                !candidateValue.TryGetEntity(out Entity target) || target == Entity.Null ||
                !entityManager.Exists(target) ||
                !entityManager.HasComponent<UnitInteractableComponent>(target))
                return false;

            UnitInteractableComponent interactable = entityManager.GetComponentData<UnitInteractableComponent>(target);
            if (interactable.HidePrompt != 0 ||
                !GameInteractionTargetUtility.IsAvailable(entityManager, target, interactable))
                return false;
            if (!entityManager.Exists(target) ||
                !entityManager.HasComponent<LocalToWorld>(target))
            {
                return false;
            }

            if (entityManager.HasComponent<DestroyEntityFlag>(target) &&
                entityManager.IsComponentEnabled<DestroyEntityFlag>(target))
            {
                return false;
            }

            LocalToWorld localToWorld = entityManager.GetComponentData<LocalToWorld>(target);
            if (!TryResolveDisplayName(
                    entityManager,
                    target,
                    interactable.Data,
                    out displayName,
                    out worldYOffset))
                return false;

            if (string.IsNullOrWhiteSpace(displayName))
                return false;

            SpriteRenderer renderer = entityManager.HasComponent<SpriteRenderer>(target)
                ? entityManager.GetComponentObject<SpriteRenderer>(target)
                : null;
            worldPosition = ResolveAnchor(renderer, localToWorld, worldYOffset);
            worldYOffset = 0f;
            return true;
        }

        internal static Vector3 ResolveAnchor(SpriteRenderer renderer, LocalToWorld transform, float baseHeight)
        {
            // Keep the original scale-one offset; sprite padding and animation bounds
            // do not describe the intended interaction label height.
            Vector3 position = renderer != null ? renderer.transform.position : (Vector3)transform.Position;
            float scale = renderer != null
                ? Mathf.Abs(renderer.transform.lossyScale.y)
                : math.length(transform.Value.c1.xyz);
            return position + Vector3.up * (baseHeight * scale);
        }

        private bool TryResolveDisplayName(
            EntityManager entityManager,
            Entity target,
            in UnitInteractionData interaction,
            out string displayName,
            out float worldYOffset)
        {
            if (_hasCachedDisplay
                && target == _cachedTarget
                && InteractionEquals(in interaction, in _cachedInteraction))
            {
                displayName = _cachedDisplayName;
                worldYOffset = _cachedWorldYOffset;
                return true;
            }

            if (!TryBuildDisplayName(entityManager, interaction, out displayName, out worldYOffset))
            {
                InvalidateCachedDisplay();
                return false;
            }

            _cachedTarget = target;
            _cachedInteraction = interaction;
            _cachedDisplayName = displayName;
            _cachedWorldYOffset = worldYOffset;
            _hasCachedDisplay = true;
            return true;
        }

        private static bool InteractionEquals(
            in UnitInteractionData left,
            in UnitInteractionData right)
        {
            return left.Kind == right.Kind
                && left.DataId == right.DataId
                && left.Amount == right.Amount
                && left.Variant == right.Variant;
        }

        private void HandleLanguageChanged()
        {
            InvalidateCachedDisplay();
        }

        private void InvalidateCachedDisplay()
        {
            _cachedTarget = Entity.Null;
            _cachedInteraction = default;
            _cachedDisplayName = string.Empty;
            _cachedWorldYOffset = DefaultWorldYOffset;
            _hasCachedDisplay = false;
        }

        private bool TryGetPromptPlayer(EntityManager entityManager, out Entity player)
        {
            player = Entity.Null;
            if (!GameWorldContextUtility.TryGet(entityManager, out GameWorldContextComponent context))
                return false;

            if (_playerWorld != entityManager.World)
            {
                _playerWorld = entityManager.World;
                _cachedPlayer = Entity.Null;
                InvalidateCachedDisplay();
            }

            if (IsPromptPlayer(entityManager, _cachedPlayer, context))
            {
                player = _cachedPlayer;
                return true;
            }

            _cachedPlayer = Entity.Null;
            if (!GameRuntimeStateUtility.TryGetPlayerEntity(entityManager, out Entity selected) ||
                !IsPromptPlayer(entityManager, selected, context))
                return false;

            _cachedPlayer = player = selected;
            return true;
        }

        private static bool IsPromptPlayer(
            EntityManager entityManager, Entity player, in GameWorldContextComponent context)
        {
            if (player == Entity.Null || !entityManager.Exists(player) ||
                !entityManager.HasComponent<PlayerInputComponent>(player) ||
                !entityManager.HasComponent<UnitVariableComponent>(player) ||
                entityManager.HasComponent<Disabled>(player) ||
                entityManager.HasComponent<UnitInitializationPendingTag>(player) ||
                entityManager.HasComponent<BattleSpectatorComponent>(player) ||
                (entityManager.HasComponent<UnitDeathComponent>(player) &&
                 entityManager.IsComponentEnabled<UnitDeathComponent>(player)) ||
                (entityManager.HasComponent<DestroyEntityFlag>(player) &&
                 entityManager.IsComponentEnabled<DestroyEntityFlag>(player)))
                return false;

            if (context.Role == GameWorldRole.Client)
                return entityManager.HasComponent<NetworkPlayerComponent>(player);

            if (!entityManager.HasComponent<UnitFactionComponent>(player) ||
                !UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(player).Value))
                return false;

            if (context.SceneMode == GameSceneMode.None)
                return true;

            bool combatScene = context.SceneMode is GameSceneMode.Dungeon or GameSceneMode.Training;
            return entityManager.HasComponent<UnitSkillReleaseComponent>(player) == combatScene;
        }

        private static bool TryBuildDisplayName(
            EntityManager entityManager,
            in UnitInteractionData interaction,
            out string displayName,
            out float worldYOffset)
        {
            worldYOffset = DefaultWorldYOffset;
            switch (interaction.Kind)
            {
                case InteractionKind.Drop:
                    displayName = BuildDropDisplayName(interaction);
                    return true;

                case InteractionKind.Treasure:
                    displayName = $"E {LocalizationComponent.Instance.Get(TreasureDisplayNameKey)}";
                    return true;

                case InteractionKind.Npc:
                    worldYOffset = CharacterWorldYOffset;
                    NPCData npcData = DataComponent.Instance.Get<NPCData>(interaction.DataId);
                    string npcName = npcData?.DisplayName;
                    if (string.IsNullOrWhiteSpace(npcName))
                        npcName = npcData?.NPC;
                    if (string.IsNullOrWhiteSpace(npcName))
                        npcName = "NPC";

                    displayName = $"E {npcName}";
                    return true;

                default:
                    displayName = string.Empty;
                    return false;
            }
        }

        private static string BuildDropDisplayName(in UnitInteractionData drop)
        {
            string name;
            if ((DropRewardType)drop.Variant == DropRewardType.Money)
            {
                string moneyDisplayName = LocalizationComponent.Instance.Get(MoneyDisplayNameKey);
                name = drop.Amount > 1 ? $"{moneyDisplayName} x{drop.Amount}" : moneyDisplayName;
                return $"E {name}";
            }

            ItemData itemData = DataComponent.Instance.Get<ItemData>(drop.DataId);
            name = itemData?.Name;
            if (string.IsNullOrWhiteSpace(name))
                name = $"Item {drop.DataId}";

            if (drop.Amount > 1)
                name = $"{name} x{drop.Amount}";

            return $"E {name}";
        }

        private void SetVisible(bool visible)
        {
            _model?.SetVisible(visible);
        }
    }
}
