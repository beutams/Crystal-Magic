using System;
using CrystalMagic.Core;

public class SaveUI_SaveItemView : UISubView<SaveUI_SaveItemData>
{
    public event Action<int> Clicked;
    public event Action<int> DeleteClicked;

    public int SlotIndex { get; private set; }

    private bool _hasRecord;
    private bool _buttonEventsBound;

    public void Render(int slotIndex, SaveRecord record)
    {
        Rebind();
        EnsureButtonEventsBound();
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

    private void EnsureButtonEventsBound()
    {
        if (_buttonEventsBound)
            return;

        GetComponent<ButtonPlus>().onClick.AddListener(OnClicked);
        UI.Open_Delete.ButtonPlus.onClick.AddListener(OnDeleteClicked);
        _buttonEventsBound = true;
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
