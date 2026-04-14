using System;
using CrystalMagic.Core;

public class SaveUI_SaveItemView : UISubView<SaveUI_SaveItemData>
{
    public event Action<int> Clicked;
    public event Action<int> DeleteClicked;

    public int SlotIndex { get; private set; }

    private ButtonPlus _buttonPlus;
    private ButtonPlus _deleteButtonPlus;
    private bool _hasRecord;

    protected override void Awake()
    {
        base.Awake();
        CacheButton();
    }

    private void OnEnable()
    {
        CacheButton();
        _buttonPlus.onClick.AddListener(OnClicked);
        _deleteButtonPlus.onClick.AddListener(OnDeleteClicked);
    }

    private void OnDisable()
    {
        _buttonPlus.onClick.RemoveListener(OnClicked);
        _deleteButtonPlus.onClick.RemoveListener(OnDeleteClicked);
    }

    public void Render(int slotIndex, SaveRecord record)
    {
        Rebind();
        SlotIndex = slotIndex;
        _hasRecord = record != null;

        UI.Open.GameObject.SetActive(_hasRecord);
        UI.Close.GameObject.SetActive(!_hasRecord);

        if (!_hasRecord)
            return;

        UI.Open_Index.TextMeshProUGUI.text = (record.SaveIndex + 1).ToString("00");
        UI.Open_CreateTime.TextMeshProUGUI.text = record.GetFormattedTime();
        UI.Open_Money.TextMeshProUGUI.text = record.StashMoney.ToString();
    }

    private void CacheButton()
    {
        _buttonPlus = GetComponent<ButtonPlus>();
        _deleteButtonPlus = UI.Open_DeleteBtn.ButtonPlus;
    }

    private void OnClicked()
    {
        Clicked?.Invoke(SlotIndex);
    }

    private void OnDeleteClicked()
    {
        if (!_hasRecord)
            return;

        DeleteClicked?.Invoke(SlotIndex);
    }
}
