using System;
using System.Collections.Generic;

namespace CrystalMagic.UI
{
    public sealed class UIProperty<T>
    {
        private T _value;

        public UIProperty()
        {
        }

        public UIProperty(T initialValue)
        {
            _value = initialValue;
        }

        public event Action<T> ValueChanged;

        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value))
                    return;

                _value = value;
                ValueChanged?.Invoke(_value);
            }
        }

        public void Notify()
        {
            ValueChanged?.Invoke(_value);
        }

        public IDisposable Subscribe(Action<T> listener, bool invokeImmediately = true)
        {
            if (listener == null)
                return null;

            ValueChanged += listener;
            if (invokeImmediately)
                listener.Invoke(_value);

            return new PropertySubscription(this, listener);
        }

        private void Unsubscribe(Action<T> listener)
        {
            ValueChanged -= listener;
        }

        private sealed class PropertySubscription : IDisposable
        {
            private UIProperty<T> _property;
            private Action<T> _listener;

            public PropertySubscription(UIProperty<T> property, Action<T> listener)
            {
                _property = property;
                _listener = listener;
            }

            public void Dispose()
            {
                if (_property == null || _listener == null)
                    return;

                _property.Unsubscribe(_listener);
                _property = null;
                _listener = null;
            }
        }
    }
}
