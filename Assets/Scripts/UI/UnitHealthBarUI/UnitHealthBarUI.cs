using CrystalMagic.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrystalMagic.UI
{
    public class UnitHealthBarUI : UIBase<UnitHealthBarUIData, UnitHealthBarUIModel>
    {
        private const float BuffIconSpacing = 2f;

        private RectTransform _rectTransform;
        private RectTransform _templateRectTransform;
        private RectTransform _templateMaskRectTransform;
        private float _templateMaskBaseWidth = -1f;
        private readonly Stack<BarHandle> _pooledBars = new();
        private readonly Dictionary<string, Sprite> _iconCache = new();

        protected override void OnInit()
        {
            base.OnInit();
            _rectTransform = transform as RectTransform;
            _templateRectTransform = UI?.HP.RectTransform;
            _templateMaskRectTransform = UI?.HP_BarMask.RectTransform;
            CacheTemplateWidth();
            NormalizeRoot();

            if (_templateRectTransform != null)
                _templateRectTransform.gameObject.SetActive(false);
        }

        public void PrepareForFloatingRoot()
        {
            EnsureInitialized();
            NormalizeRoot();
        }

        public BarHandle AcquireBar()
        {
            EnsureInitialized();
            CacheTemplateWidth();
            if (_templateRectTransform == null || _templateMaskRectTransform == null)
                return null;

            BarHandle handle = _pooledBars.Count > 0 ? _pooledBars.Pop() : CreateBarHandle();
            if (handle?.Root == null || handle.BarMask == null)
                return null;

            handle.Root.gameObject.SetActive(true);
            return handle;
        }

        public void ReleaseBar(BarHandle handle)
        {
            if (handle?.Root == null)
                return;

            handle.Root.gameObject.SetActive(false);
            _pooledBars.Push(handle);
        }

        public void UpdateBar(BarHandle handle, float currentHealth, float maxHealth, Vector2 anchoredPosition, bool visible)
        {
            if (handle?.Root == null || handle.BarMask == null)
                return;

            handle.Root.anchoredPosition = anchoredPosition;
            if (handle.BaseWidth > 0f)
            {
                float ratio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
                handle.BarMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, handle.BaseWidth * ratio);
            }

            if (handle.Root.gameObject.activeSelf != visible)
                handle.Root.gameObject.SetActive(visible);
        }

        public void UpdateBuffIcons(BarHandle handle, IReadOnlyList<UnitHealthBarBuffDisplayData> buffs)
        {
            if (handle?.BuffRoot == null || handle.BuffIconTemplate == null)
                return;

            int visibleCount = buffs?.Count ?? 0;
            EnsureBuffIconCapacity(handle, visibleCount);
            for (int i = 0; i < handle.ActiveBuffIcons.Count; i++)
            {
                BuffIconHandle iconHandle = handle.ActiveBuffIcons[i];
                bool shouldShow = i < visibleCount;
                if (iconHandle?.Root == null || iconHandle.Icon == null)
                    continue;

                if (!shouldShow)
                {
                    if (iconHandle.Root.gameObject.activeSelf)
                        iconHandle.Root.gameObject.SetActive(false);
                    continue;
                }

                UnitHealthBarBuffDisplayData buff = buffs[i];
                iconHandle.Root.anchoredPosition = new Vector2(i * (iconHandle.Size + BuffIconSpacing), 0f);
                iconHandle.Icon.sprite = LoadIcon(buff.IconPath);
                if (!iconHandle.Root.gameObject.activeSelf)
                    iconHandle.Root.gameObject.SetActive(true);
            }

            if (handle.BuffRoot.gameObject.activeSelf != (visibleCount > 0))
                handle.BuffRoot.gameObject.SetActive(visibleCount > 0);
        }

        public void SetBarVisible(BarHandle handle, bool visible)
        {
            if (handle?.Root == null)
                return;

            if (handle.Root.gameObject.activeSelf != visible)
                handle.Root.gameObject.SetActive(visible);
        }

        protected override void RefreshView()
        {
        }

        private void NormalizeRoot()
        {
            _rectTransform ??= transform as RectTransform;
            if (_rectTransform == null)
                return;

            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.anchoredPosition = Vector2.zero;
            _rectTransform.sizeDelta = Vector2.zero;
            _rectTransform.localScale = Vector3.one;
            _rectTransform.localRotation = Quaternion.identity;
        }

        private void CacheTemplateWidth()
        {
            if (_templateMaskBaseWidth > 0f || _templateMaskRectTransform == null)
                return;

            _templateMaskBaseWidth = _templateMaskRectTransform.rect.width;
            if (_templateMaskBaseWidth <= 0f)
                _templateMaskBaseWidth = _templateMaskRectTransform.sizeDelta.x;
        }

        private BarHandle CreateBarHandle()
        {
            if (_templateRectTransform == null)
                return null;

            GameObject clone = Instantiate(_templateRectTransform.gameObject, _templateRectTransform.parent, false);
            clone.name = _templateRectTransform.gameObject.name;
            RectTransform root = clone.transform as RectTransform;
            RectTransform barMask = clone.transform.Find("BarMask") as RectTransform;
            RectTransform buffRoot = clone.transform.Find("BuffRoot") as RectTransform;
            RectTransform buffIconTemplate = clone.transform.Find("BuffRoot/BuffIcon") as RectTransform;
            if (root == null || barMask == null)
            {
                Destroy(clone);
                return null;
            }

            float baseWidth = barMask.rect.width;
            if (baseWidth <= 0f)
                baseWidth = barMask.sizeDelta.x;

            if (buffRoot != null)
                buffRoot.gameObject.SetActive(false);

            if (buffIconTemplate != null)
                buffIconTemplate.gameObject.SetActive(false);

            clone.SetActive(true);
            return new BarHandle(root, barMask, buffRoot, buffIconTemplate, baseWidth > 0f ? baseWidth : _templateMaskBaseWidth);
        }

        private void EnsureBuffIconCapacity(BarHandle handle, int itemCount)
        {
            if (handle == null)
                return;

            while (handle.ActiveBuffIcons.Count < itemCount)
            {
                BuffIconHandle iconHandle = handle.PooledBuffIcons.Count > 0
                    ? handle.PooledBuffIcons.Pop()
                    : CreateBuffIconHandle(handle);
                if (iconHandle == null)
                    return;

                handle.ActiveBuffIcons.Add(iconHandle);
            }

            while (handle.ActiveBuffIcons.Count > itemCount)
            {
                int lastIndex = handle.ActiveBuffIcons.Count - 1;
                BuffIconHandle iconHandle = handle.ActiveBuffIcons[lastIndex];
                handle.ActiveBuffIcons.RemoveAt(lastIndex);
                if (iconHandle?.Root != null)
                    iconHandle.Root.gameObject.SetActive(false);
                handle.PooledBuffIcons.Push(iconHandle);
            }
        }

        private BuffIconHandle CreateBuffIconHandle(BarHandle handle)
        {
            if (handle?.BuffRoot == null || handle.BuffIconTemplate == null)
                return null;

            GameObject clone = Instantiate(handle.BuffIconTemplate.gameObject, handle.BuffRoot, false);
            clone.name = handle.BuffIconTemplate.gameObject.name;
            RectTransform root = clone.transform as RectTransform;
            Image image = clone.GetComponent<Image>();
            if (root == null || image == null)
            {
                Destroy(clone);
                return null;
            }

            float size = root.rect.width;
            if (size <= 0f)
                size = root.sizeDelta.x;

            root.gameObject.SetActive(false);
            return new BuffIconHandle(root, image, size > 0f ? size : 10f);
        }

        private Sprite LoadIcon(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath))
                return null;

            if (_iconCache.TryGetValue(iconPath, out Sprite cachedSprite))
                return cachedSprite;

            Sprite sprite = LoadManagedResource<Sprite>(iconPath);
            _iconCache[iconPath] = sprite;
            return sprite;
        }

        public sealed class BarHandle
        {
            public RectTransform Root { get; }
            public RectTransform BarMask { get; }
            public RectTransform BuffRoot { get; }
            public RectTransform BuffIconTemplate { get; }
            public float BaseWidth { get; }
            public List<BuffIconHandle> ActiveBuffIcons { get; } = new();
            public Stack<BuffIconHandle> PooledBuffIcons { get; } = new();

            public BarHandle(RectTransform root, RectTransform barMask, RectTransform buffRoot, RectTransform buffIconTemplate, float baseWidth)
            {
                Root = root;
                BarMask = barMask;
                BuffRoot = buffRoot;
                BuffIconTemplate = buffIconTemplate;
                BaseWidth = baseWidth;
            }
        }

        public sealed class BuffIconHandle
        {
            public RectTransform Root { get; }
            public Image Icon { get; }
            public float Size { get; }

            public BuffIconHandle(RectTransform root, Image icon, float size)
            {
                Root = root;
                Icon = icon;
                Size = size;
            }
        }
    }

    public sealed class UnitHealthBarBuffDisplayData
    {
        public int BuffId;
        public int StackCount;
        public int SourceSkillId;
        public string IconPath;
    }
}
