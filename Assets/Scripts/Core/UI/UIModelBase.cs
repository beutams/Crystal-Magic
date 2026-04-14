using System;

namespace CrystalMagic.UI
{
    public interface IUIOpenDataReceiver<in TData>
    {
        void SetOpenData(TData data);
    }

    public abstract class UIModelBase : IDisposable
    {
        public virtual void Dispose()
        {
        }
    }
}
