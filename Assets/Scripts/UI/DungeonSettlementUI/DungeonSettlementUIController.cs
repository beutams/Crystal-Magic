using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.UI
{
    public sealed class DungeonSettlementUIController : UIControllerBase<DungeonSettlementUI, DungeonSettlementUIModel>
    {
        public DungeonSettlementUIController(DungeonSettlementUI view, DungeonSettlementUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Bindings.Bind(() => View.ConfirmClicked += OnConfirmClicked, () => View.ConfirmClicked -= OnConfirmClicked);
            Bindings.Bind(() => View.ItemClicked += Model.SelectItem, () => View.ItemClicked -= Model.SelectItem);
            List<DungeonSettlementItemDisplayData> items = new();
            if (Model.PendingResult?.Items != null)
                foreach (InventoryItemData item in Model.PendingResult.Items)
                {
                    if (item == null || item.IsEmpty)
                        continue;
                    ItemData config = DataComponent.Instance.Get<ItemData>(item.ItemId);
                    items.Add(new DungeonSettlementItemDisplayData
                    {
                        ItemId = item.ItemId,
                        Count = item.Quantity,
                        Name = config?.Name ?? $"物品 #{item.ItemId}",
                        IconPath = config?.IconPath ?? string.Empty,
                    });
                }
            Model.SetItems(items);
        }

        private void OnConfirmClicked()
        {
            if (!Model.TryBeginReturn())
                return;
            if (Model.ConfirmAction?.Invoke() != true)
            {
                Model.SetSaveFailed();
                return;
            }
            View.Close();
        }
    }
}
