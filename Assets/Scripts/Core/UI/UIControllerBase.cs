using System;
using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public abstract class UIControllerBase : IDisposable
    {
        private bool _opened;
        private bool _disposed;

        protected UIBindingScope Bindings { get; } = new();

        internal void Open()
        {
            if (_disposed || _opened)
                return;

            _opened = true;
            OnOpen();
        }

        internal void Close()
        {
            if (!_opened)
                return;

            OnClose();
            Bindings.Clear();
            _opened = false;
        }

        protected virtual void OnOpen()
        {
        }

        protected virtual void OnClose()
        {
        }

        protected virtual void OnDispose()
        {
        }

        protected IDisposable Bind<T>(UIProperty<T> property, Action<T> handler, bool invokeImmediately = true)
        {
            return Bindings.Bind(property, handler, invokeImmediately);
        }

        protected IDisposable BindModelChanged(UIModelBase model, Action handler, bool invokeImmediately = true)
        {
            if (model == null || handler == null)
                return null;

            if (invokeImmediately)
                handler.Invoke();

            void OnModelChanged()
            {
                handler.Invoke();
            }

            model.Changed += OnModelChanged;
            IDisposable binding = new ModelChangedBinding(model, OnModelChanged);
            Bindings.Add(binding);
            return binding;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_opened)
                Close();

            OnDispose();
            Bindings.Dispose();
            _disposed = true;
        }

        private sealed class ModelChangedBinding : IDisposable
        {
            private UIModelBase _model;
            private Action _handler;

            public ModelChangedBinding(UIModelBase model, Action handler)
            {
                _model = model;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_model == null || _handler == null)
                    return;

                _model.Changed -= _handler;
                _model = null;
                _handler = null;
            }
        }
    }

    public abstract class UIControllerBase<TView, TModel> : UIControllerBase
        where TView : UIBase
        where TModel : UIModelBase
    {
        protected TView View { get; }
        protected TModel Model { get; }

        protected UIControllerBase(TView view, TModel model)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }
    }
}
