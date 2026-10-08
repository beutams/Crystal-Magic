using CrystalMagic.Core;
using CrystalMagic.Game.Data;
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

        public void Initialize()
        {
            if (_initialized)
                return;

            ResolveFloatingRoot();
            EnsurePromptView();
            SetVisible(false);
            LocalizationComponent.LanguageChanged += HandleLanguageChanged;
            // Project only after camera follow, shake and companion transforms have updated.
            Canvas.preWillRenderCanvases += RefreshPrompt;
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
            Canvas.preWillRenderCanvases -= RefreshPrompt;
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
