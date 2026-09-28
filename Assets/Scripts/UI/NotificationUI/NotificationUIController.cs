using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class NotificationUIController : UIControllerBase<NotificationUI, NotificationUIModel>
    {
        public NotificationUIController(NotificationUI view, NotificationUIModel model) : base(view, model) { }
        protected override void OnOpen()
        {
            View.BindModel(Model);
            Bindings.Bind(() => View.Completed += Model.Complete, () => View.Completed -= Model.Complete);
            BindEvent<PickupFeedbackEvent>(OnPickup);
            BindEvent<NotificationSignalEvent>(OnSignal);
        }
        protected override void OnClose() => Model.Clear();

        private void OnSignal(NotificationSignalEvent signal)
        {
            NotificationTemplate template = View.FindTemplate(signal.Key);
            if (template == null) return;
            string text = string.IsNullOrEmpty(template.TextKey) ? string.Empty : LocalizationComponent.Instance.Get(template.TextKey);
            Model.Push(new NotificationRequest(template.Key, text, template.Channel, template.Policy,
                signal.Values.x, signal.Values.y, template.Maximum, signal.Scope, Mathf.RoundToInt(signal.Values.z),
                template.MaxVisible, template.MaxQueued));
        }

        private void OnPickup(PickupFeedbackEvent signal)
        {
            NotificationTemplate template = View.FindTemplate("pickup");
            if (template == null) return;
            string text;
            Color tint = Color.white;
            if (signal.Type == PickupFeedbackType.Item)
            {
                ItemData item = DataComponent.Instance.Get<ItemData>(signal.ItemId);
                text = item != null && !string.IsNullOrWhiteSpace(item.Name) ? item.Name : $"Item {signal.ItemId}";
                if (signal.Amount > 1) text += $" x{signal.Amount}";
            }
            else if (signal.Type == PickupFeedbackType.Money)
            {
                text = LocalizationComponent.Instance.Get("world.drop.money");
                if (signal.Amount > 1) text += $" x{signal.Amount}";
            }
            else
            {
                text = LocalizationComponent.Instance.Get("ui.shop.inventory_full");
                tint = new Color(1, 0.45f, 0.35f, 1);
            }
            Model.Push(new NotificationRequest(template.Key, text, template.Channel, template.Policy,
                maxVisible: template.MaxVisible, maxQueued: template.MaxQueued, tint: tint));
        }
    }
}
