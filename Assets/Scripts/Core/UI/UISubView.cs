using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core
{
    public abstract class UISubViewBase : MonoBehaviour
    {
        public abstract void Rebind();

        protected TResource LoadManagedResource<TResource>(string path) where TResource : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string ownerKey = UIComponent.Instance.GetResourceOwnerKey(this);
            return string.IsNullOrWhiteSpace(ownerKey)
                ? ResourceComponent.Instance.Load<TResource>(path)
                : ResourceComponent.Instance.Load<TResource>(path, ownerKey);
        }

        public static TView AcquireFromPool<TView>(TView templateView, Transform parent) where TView : UISubViewBase
        {
            if (templateView == null)
                return null;

            string ownerKey = parent != null ? UIComponent.Instance.GetResourceOwnerKey(parent) : string.Empty;
            GameObject instance = string.IsNullOrWhiteSpace(ownerKey)
                ? PoolComponent.Instance.Get(templateView.gameObject)
                : PoolComponent.Instance.Get(templateView.gameObject, ownerKey);
            if (instance == null)
                return null;

            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(parent, false);
            instanceTransform.SetAsLastSibling();
            instance.name = templateView.gameObject.name;

            TView view = instance.GetComponent<TView>();
            view?.Rebind();
            return view;
        }

        public static void EnsurePoolCapacity(UISubViewBase templateView, int maxSize, int initialSize = 0)
        {
            if (templateView == null || PoolComponent.Instance == null)
                return;

            PoolComponent.Instance.EnsurePool(templateView.gameObject, maxSize, initialSize);
        }

        public static void ReleaseToPool(UISubViewBase view)
        {
            if (view == null)
                return;

            PoolComponent.Instance.Release(view.gameObject);
        }

        public static void ReleaseAllToPool<TView>(IList<TView> views) where TView : UISubViewBase
        {
            if (views == null)
                return;

            for (int i = views.Count - 1; i >= 0; i--)
                ReleaseToPool(views[i]);

            views.Clear();
        }
    }

    public abstract class UISubView<TData> : UISubViewBase where TData : UIData, new()
    {
        private TData _ui;

        public TData UI
        {
            get
            {
                EnsureBound();
                return _ui;
            }
        }

        protected virtual void Awake()
        {
            EnsureBound();
        }

        public override void Rebind()
        {
            _ui = new TData();
            _ui.Bind(transform);
        }

        private void EnsureBound()
        {
            if (_ui != null)
                return;

            Rebind();
        }
    }
}
