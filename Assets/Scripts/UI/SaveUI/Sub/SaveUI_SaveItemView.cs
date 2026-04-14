using System;
using CrystalMagic.Core;

public class SaveUI_SaveItemView : UISubView<SaveUI_SaveItemData>
{
    public event Action<int> Clicked;

    public int SlotIndex { get; private set; }

    private ButtonPlus _buttonPlus;

    protected override void Awake()
    {
        base.Awake();
        CacheButton();
    }

    private void OnEnable()
    {
        CacheButton();
        _buttonPlus.onClick.AddListener(OnClicked);
    }

    private void OnDisable()
    {
        _buttonPlus.onClick.RemoveListener(OnClicked);
    }

    public void Render(int slotIndex, SaveRecord record)
    {
        Rebind();
        SlotIndex = slotIndex;

        UI.Index.TextMeshProUGUI.text = (slotIndex + 1).ToString("00");

        UI.CreateTime.TextMeshProUGUI.text = record != null
            ? record.GetFormattedTime()
            : "Empty";

        UI.Money.TextMeshProUGUI.text = record != null
            ? record.StashMoney.ToString()
            : "-";
    }

    private void CacheButton()
    {
        _buttonPlus = GetComponent<ButtonPlus>();
    }

    private void OnClicked()
    {
        Clicked?.Invoke(SlotIndex);
    }
}
