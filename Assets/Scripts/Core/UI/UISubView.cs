using UnityEngine;

namespace CrystalMagic.Core
{
    public abstract class UISubViewBase : MonoBehaviour
    {
        public abstract void Rebind();
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
