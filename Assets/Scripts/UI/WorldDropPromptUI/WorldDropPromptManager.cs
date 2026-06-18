using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using TMPro;
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
        private const string MoneyDisplayName = "\u91d1\u5e01";
        private const string TreasureDisplayName = "\u5b9d\u7bb1";

        private RectTransform _rootRect;
        private Camera _currentCamera;
        private RectTransform _promptRoot;
        private RectTransform _labelRect;
        private TextMeshProUGUI _label;
        private World _runtimeQueryWorld;
        private EntityQuery _runtimeQuery;
        private bool _initialized;

        public void Initialize()
        {
            if (_initialized)
                return;

            ResolveFloatingRoot();
            EnsurePromptView();
            SetVisible(false);
            _initialized = true;
        }

        public void Tick()
        {
            if (!_initialized)
                return;

            if (!ResolveFloatingRoot() || !EnsurePromptView())
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

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, screenPosition, _currentCamera, out Vector2 localPoint))
            {
                SetVisible(false);
                return;
            }

            _label.text = displayName;
            _labelRect.anchoredPosition = localPoint;
            SetVisible(true);
        }

        public void Dispose()
        {
            ReleaseRuntimeQuery();

            if (_promptRoot != null)
                Object.Destroy(_promptRoot.gameObject);

            _promptRoot = null;
            _labelRect = null;
            _label = null;
            _rootRect = null;
            _currentCamera = null;
            _initialized = false;
        }

        private bool ResolveFloatingRoot()
        {
            UIGroup group = UIComponent.Instance.GetGroup<UIGroup>(GroupName);
            if (group == null)
                return false;

            _rootRect = group.transform as RectTransform;
            Canvas canvas = group.GetComponent<Canvas>();
            _currentCamera = canvas != null ? canvas.worldCamera : CameraComponent.Instance.Current;
            return _rootRect != null && _currentCamera != null;
        }

        private bool EnsurePromptView()
        {
            if (_promptRoot != null && _labelRect != null && _label != null)
                return true;

            if (_rootRect == null)
                return false;

            GameObject rootObject = new("WorldDropPrompt", typeof(RectTransform));
            _promptRoot = rootObject.GetComponent<RectTransform>();
            _promptRoot.SetParent(_rootRect, false);
            _promptRoot.anchorMin = Vector2.zero;
            _promptRoot.anchorMax = Vector2.one;
            _promptRoot.offsetMin = Vector2.zero;
            _promptRoot.offsetMax = Vector2.zero;
            _promptRoot.pivot = new Vector2(0.5f, 0.5f);
            _promptRoot.localScale = Vector3.one;
            _promptRoot.localRotation = Quaternion.identity;

            GameObject labelObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            _labelRect = labelObject.GetComponent<RectTransform>();
            _labelRect.SetParent(_promptRoot, false);
            _labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _labelRect.pivot = new Vector2(0.5f, 0.5f);
            _labelRect.sizeDelta = new Vector2(320f, 40f);
            _labelRect.localScale = Vector3.one;
            _labelRect.localRotation = Quaternion.identity;

            _label = labelObject.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset fontAsset = TMP_Settings.defaultFontAsset;
            if (fontAsset == null)
                fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (fontAsset != null)
                _label.font = fontAsset;
            _label.fontSize = 24f;
            _label.fontStyle = FontStyles.Bold;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = Color.white;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.outlineWidth = 0.18f;
            _label.outlineColor = new Color(0f, 0f, 0f, 0.9f);
            _label.raycastTarget = false;

            return true;
        }

        private bool TryGetPromptTarget(out float3 worldPosition, out string displayName, out float worldYOffset)
        {
            worldPosition = float3.zero;
            displayName = string.Empty;
            worldYOffset = DefaultWorldYOffset;

            World world = World.DefaultGameObjectInjectionWorld;
            if (!EnsureRuntimeQuery(world))
                return false;

            if (_runtimeQuery.IsEmptyIgnoreFilter)
                return false;

            PlayerInteractionRuntimeComponent runtime = _runtimeQuery.GetSingleton<PlayerInteractionRuntimeComponent>();
            if (runtime.CurrentTarget == Entity.Null || runtime.CurrentKind == PlayerInteractionKind.None)
                return false;

            EntityManager entityManager = world.EntityManager;
            Entity target = runtime.CurrentTarget;
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
            if (!TryBuildDisplayName(entityManager, target, runtime.CurrentKind, out displayName, out worldYOffset))
                return false;

            if (string.IsNullOrWhiteSpace(displayName))
                return false;

            worldPosition = localToWorld.Position;
            return true;
        }

        private bool EnsureRuntimeQuery(World world)
        {
            if (world == null || !world.IsCreated)
                return false;

            if (_runtimeQueryWorld == world)
                return true;

            ReleaseRuntimeQuery();
            _runtimeQueryWorld = world;
            _runtimeQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerInteractionRuntimeComponent>());
            return true;
        }

        private void ReleaseRuntimeQuery()
        {
            if (_runtimeQueryWorld == null || !_runtimeQueryWorld.IsCreated)
            {
                _runtimeQueryWorld = null;
                _runtimeQuery = default;
                return;
            }

            _runtimeQuery.Dispose();
            _runtimeQueryWorld = null;
            _runtimeQuery = default;
        }

        private static bool TryBuildDisplayName(
            EntityManager entityManager,
            Entity target,
            PlayerInteractionKind interactionKind,
            out string displayName,
            out float worldYOffset)
        {
            worldYOffset = DefaultWorldYOffset;
            switch (interactionKind)
            {
                case PlayerInteractionKind.Drop:
                    if (!entityManager.HasComponent<WorldDropComponent>(target))
                    {
                        displayName = string.Empty;
                        return false;
                    }

                    WorldDropComponent drop = entityManager.GetComponentData<WorldDropComponent>(target);
                    displayName = BuildDropDisplayName(drop);
                    return true;

                case PlayerInteractionKind.Treasure:
                    displayName = $"E {TreasureDisplayName}";
                    return true;

                case PlayerInteractionKind.Npc:
                    if (!entityManager.HasComponent<NPCInteractableComponent>(target))
                    {
                        displayName = string.Empty;
                        return false;
                    }

                    worldYOffset = entityManager.HasComponent<DungeonExitComponent>(target)
                        ? DefaultWorldYOffset
                        : CharacterWorldYOffset;
                    NPCInteractableComponent interactable = entityManager.GetComponentData<NPCInteractableComponent>(target);
                    NPCData npcData = DataComponent.Instance.Get<NPCData>(interactable.NpcId);
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

        private static string BuildDropDisplayName(in WorldDropComponent drop)
        {
            string name;
            if (drop.DropType == DropRewardType.Money)
            {
                name = drop.Amount > 1 ? $"{MoneyDisplayName} x{drop.Amount}" : MoneyDisplayName;
                return $"E {name}";
            }

            ItemData itemData = DataComponent.Instance.Get<ItemData>(drop.ItemId);
            name = itemData?.Name;
            if (string.IsNullOrWhiteSpace(name))
                name = $"Item {drop.ItemId}";

            if (drop.Amount > 1)
                name = $"{name} x{drop.Amount}";

            return $"E {name}";
        }

        private void SetVisible(bool visible)
        {
            if (_promptRoot == null || _promptRoot.gameObject.activeSelf == visible)
                return;

            _promptRoot.gameObject.SetActive(visible);
        }
    }
}
