using System;
using System.Collections.Generic;

namespace CrystalMagic.UI
{
    public sealed class UIBindingScope : IDisposable
    {
        private readonly List<IDisposable> _bindings = new();

        public IDisposable Bind<T>(UIProperty<T> property, Action<T> handler, bool invokeImmediately = true)
        {
            if (property == null || handler == null)
                return null;

            IDisposable binding = property.Subscribe(handler, invokeImmediately);
            _bindings.Add(binding);
            return binding;
        }

        public IDisposable Bind(Action subscribe, Action unsubscribe)
        {
            if (subscribe == null || unsubscribe == null)
                return null;

            subscribe.Invoke();
            IDisposable binding = new CallbackDisposable(unsubscribe);
            _bindings.Add(binding);
            return binding;
        }

        public void Add(IDisposable binding)
        {
            if (binding != null)
                _bindings.Add(binding);
        }

        public void Clear()
        {
            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                _bindings[i]?.Dispose();
            }

            _bindings.Clear();
        }

        public void Dispose()
        {
            Clear();
        }

        private sealed class CallbackDisposable : IDisposable
        {
            private Action _disposeAction;

            public CallbackDisposable(Action disposeAction)
            {
                _disposeAction = disposeAction;
            }

            public void Dispose()
            {
                _disposeAction?.Invoke();
                _disposeAction = null;
            }
        }
    }
}
