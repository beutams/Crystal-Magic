using CrystalMagic.Core;
using System.Collections.Generic;

namespace CrystalMagic.UI
{
    public sealed class NotificationUIModel : UIModelBase
    {
        private readonly NotificationQueue _queue = new();
        public override string ChangedEventName => "NotificationUIModel.Changed";
        public IReadOnlyList<NotificationRecord> Active => _queue.Active;
        public void Push(in NotificationRequest request) { _queue.Enqueue(request); Changed(); }
        public void Complete(int id) { _queue.Complete(id); Changed(); }
        public void Clear() { _queue.Clear(); Changed(); }
        private void Changed() => EventComponent.Instance.Publish(new CommonGameEvent(ChangedEventName, this));
        public override void Dispose() => _queue.Clear();
    }
}
