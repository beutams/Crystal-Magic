using CrystalMagic.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.UI
{
    public class UnitHealthBarUI : UIBase<UnitHealthBarUIData, UnitHealthBarUIModel>
    {
        private RectTransform _rectTransform;
        private RectTransform _templateRectTransform;
        private RectTransform _templateMaskRectTransform;
        private float _templateMaskBaseWidth = -1f;
        private readonly Stack<BarHandle> _pooledBars = new();

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
            if (root == null || barMask == null)
            {
                Destroy(clone);
                return null;
            }

            float baseWidth = barMask.rect.width;
            if (baseWidth <= 0f)
                baseWidth = barMask.sizeDelta.x;

            clone.SetActive(true);
            return new BarHandle(root, barMask, baseWidth > 0f ? baseWidth : _templateMaskBaseWidth);
        }

        public sealed class BarHandle
        {
            public RectTransform Root { get; }
            public RectTransform BarMask { get; }
            public float BaseWidth { get; }

            public BarHandle(RectTransform root, RectTransform barMask, float baseWidth)
            {
                Root = root;
                BarMask = barMask;
                BaseWidth = baseWidth;
            }
        }
    }
}
