using CrystalMagic.Core;
using System.Collections.Generic;

namespace CrystalMagic.UI
{
    public sealed class DungeonSettlementUIOpenData
    {
        public DungeonSettlementResult Result;
        public bool SaveFailed;
        public System.Func<bool> ConfirmAction;
    }

    public sealed class DungeonSettlementUIModel : UIModelBase, IUIOpenDataReceiver<DungeonSettlementUIOpenData>
    {
        public const string DataChangedEventName = "DungeonSettlementUIModel.DataChanged";

        public override string ChangedEventName => DataChangedEventName;

        private readonly List<DungeonSettlementItemDisplayData> _items = new();
        public IReadOnlyList<DungeonSettlementItemDisplayData> Items => _items;
        public bool IsSuccess { get; private set; }
        public bool HasCompleteHistory { get; private set; }
        public int ReachedFloor { get; private set; }
        public long Money { get; private set; }
        public int SelectedIndex { get; private set; } = -1;
        public bool IsReturning { get; private set; }
        public bool SaveFailed { get; private set; }
        public string Title => IsSuccess ? "成功归来" : "探索失败";
        public string Summary => $"地下城探索结束 · 抵达第 {ReachedFloor} 层";
        public string Caption => $"{(IsSuccess ? "本次新获得" : "本次丢失")} · {_items.Count} 种物品";
        public string ConfirmLabel => IsReturning ? "正在返回…" : SaveFailed ? "重试保存并返回" : "返回城镇";
        public string EmptyText => IsSuccess && !HasCompleteHistory ? "旧存档未记录此前的获得明细" : IsSuccess ? "本次没有获得物品" : "没有丢失物品";
        public string Footnote => SaveFailed ? "自动存档失败，请重试保存后再退出游戏" : !IsSuccess ? "城镇仓库不受影响" : !HasCompleteHistory
            ? "旧存档：仅显示更新后记录的获得明细" : "本次获得统计包含已使用的物品";
        public string Detail => SelectedIndex >= 0 && SelectedIndex < _items.Count
            ? $"{_items[SelectedIndex].Name} × {_items[SelectedIndex].Count} · {(IsSuccess ? "本次新获得" : "已丢失")}" : "选择物品查看详情";
        public System.Func<bool> ConfirmAction { get; private set; }

        public void SetOpenData(DungeonSettlementUIOpenData data)
        {
            ConfirmAction = data?.ConfirmAction;
            DungeonSettlementResult result = data?.Result;
            PendingResult = result;
            IsSuccess = result?.IsSuccess == true;
            HasCompleteHistory = result?.HasCompleteHistory == true;
            ReachedFloor = System.Math.Max(1, result?.ReachedFloor ?? 1);
            Money = result?.Money ?? 0;
            SelectedIndex = -1;
            IsReturning = false;
            SaveFailed = data?.SaveFailed == true;
            _items.Clear();
            NotifyChanged();
        }

        public void SetItems(IEnumerable<DungeonSettlementItemDisplayData> items)
        {
            _items.Clear();
            if (items != null)
                _items.AddRange(items);
            SelectedIndex = -1;
            NotifyChanged();
        }

        public void SelectItem(int index)
        {
            if (IsReturning || index < 0 || index >= _items.Count || SelectedIndex == index)
                return;
            SelectedIndex = index;
            NotifyChanged();
        }

        public bool TryBeginReturn()
        {
            if (IsReturning)
                return false;
            IsReturning = true;
            NotifyChanged();
            return true;
        }

        public void SetSaveFailed()
        {
            IsReturning = false;
            SaveFailed = true;
            NotifyChanged();
        }

        private void NotifyChanged() => EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));

        // The controller resolves item configuration; the model retains only report state.
        public DungeonSettlementResult PendingResult { get; private set; }

        public override void Dispose()
        {
            ConfirmAction = null;
            PendingResult = null;
            _items.Clear();
        }
    }

    public sealed class DungeonSettlementItemDisplayData
    {
        public int ItemId;
        public int Count;
        public string Name;
        public string IconPath;
    }
}
