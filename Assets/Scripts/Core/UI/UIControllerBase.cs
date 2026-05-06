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

        internal void Update()
        {
            if (_disposed || !_opened)
                return;

            OnUpdate();
        }

        protected virtual void OnOpen()
        {
        }

        protected virtual void OnClose()
        {
        }

        protected virtual void OnUpdate()
        {
        }

        protected virtual void OnDispose()
        {
        }

        protected IDisposable Bind<T>(UIProperty<T> property, Action<T> handler, bool invokeImmediately = true)
        {
            return Bindings.Bind(property, handler, invokeImmediately);
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

        protected IDisposable BindEvent<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null)
                return null;

            return Bindings.Bind(
                () => SubscribeOwnedEvent(handler),
                () => EventComponent.Instance.Unsubscribe(handler));
        }

        protected IDisposable BindEvent(CommonGameEvent gameEvent, Action<CommonGameEvent> handler)
        {
            if (string.IsNullOrEmpty(gameEvent.EventName) || handler == null)
                return null;

            return Bindings.Bind(
                () => SubscribeOwnedEvent(gameEvent, handler),
                () => EventComponent.Instance.Unsubscribe(gameEvent, handler));
        }

        private void SubscribeOwnedEvent<T>(Action<T> handler) where T : IGameEvent
        {
            string ownerKey = UIComponent.Instance.GetResourceOwnerKey(View);
            if (string.IsNullOrWhiteSpace(ownerKey))
            {
                EventComponent.Instance.Subscribe(handler);
                return;
            }

            EventComponent.Instance.Subscribe(ownerKey, handler);
        }

        private void SubscribeOwnedEvent(CommonGameEvent gameEvent, Action<CommonGameEvent> handler)
        {
            string ownerKey = UIComponent.Instance.GetResourceOwnerKey(View);
            if (string.IsNullOrWhiteSpace(ownerKey))
            {
                EventComponent.Instance.Subscribe(gameEvent, handler);
                return;
            }

            EventComponent.Instance.Subscribe(ownerKey, gameEvent, handler);
        }
    }
}
