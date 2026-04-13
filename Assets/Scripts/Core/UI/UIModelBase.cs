using System;
using System.Collections.Generic;

namespace CrystalMagic.UI
{
    public abstract class UIModelBase : IDisposable
    {
        public event Action Changed;

        protected bool SetProperty<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            NotifyChanged();
            return true;
        }

        protected void NotifyChanged()
        {
            Changed?.Invoke();
        }

        public virtual void Dispose()
        {
        }
    }
}
