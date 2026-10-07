using System;
using CrystalMagic.Core;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;

public class SaveUI_SaveItemView : UISubView<SaveUI_SaveItemData>, IPointerEnterHandler, IPointerExitHandler
{
    public event System.Action<int> Clicked;
    public event System.Action<int> DeleteClicked;

    public int SlotIndex { get; private set; }

    private SaveRecord _record;
    public bool HasRecord => _record != null;

    private void OnEnable()
    {
        UI.Save.ButtonPlus.onClick.AddListener(OnClicked);
        UI.Open_Delete.ButtonPlus.onClick.AddListener(OnDeleteClicked);
        LocalizationComponent.LanguageChanged += RefreshValues;
        UI.Highlight.GameObject.SetActive(false);
    }

    private void OnDisable()
    {
        UI.Save.ButtonPlus.onClick.RemoveListener(OnClicked);
        UI.Open_Delete.ButtonPlus.onClick.RemoveListener(OnDeleteClicked);
        LocalizationComponent.LanguageChanged -= RefreshValues;
        UI.Highlight.GameObject.SetActive(false);
    }

    public void Render(int slotIndex, SaveRecord record)
    {
        SlotIndex = slotIndex;
        _record = record;

        UI.Open.GameObject.SetActive(HasRecord);
        UI.Close.GameObject.SetActive(!HasRecord);
        UI.Badge_Occupied.GameObject.SetActive(HasRecord);
        UI.Badge_Empty.GameObject.SetActive(!HasRecord);
        UI.Badge_Index.TextMeshProUGUI.text = (slotIndex + 1).ToString("00");
        UI.Badge_Index.TextMeshProUGUI.color = HasRecord
            ? new Color32(73, 51, 45, 255)
            : new Color32(120, 96, 70, 255);
        UI.Highlight.GameObject.SetActive(false);
        RefreshValues();
    }

    private void RefreshValues()
    {
        string action = LocalizationComponent.Resolve(HasRecord ? "ui.save.overwrite" : "ui.save.create");
        UI.Save_Default_Text.TextMeshProUGUI.text = action;
        UI.Save_Click_Text.TextMeshProUGUI.text = action;

        if (!HasRecord)
            return;

        UI.Open_MaxFloor.TextMeshProUGUI.text = FormatStatistic(_record.MaxFloor, "ui.load.floor_value");
        UI.Open_TotalRuns.TextMeshProUGUI.text = FormatStatistic(_record.TotalRuns, "ui.load.run_value");
        UI.Open_Money.TextMeshProUGUI.text = _record.StashMoney.ToString("N0", CultureInfo.CurrentCulture);
    }

    private static string FormatStatistic(int value, string key)
    {
        return value < 0 ? "—" : string.Format(CultureInfo.CurrentCulture, LocalizationComponent.Resolve(key), value);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        UI.Highlight.GameObject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UI.Highlight.GameObject.SetActive(false);
    }

    private void OnClicked()
    {
        Clicked?.Invoke(SlotIndex);
    }

    private void OnDeleteClicked()
    {
        if (!HasRecord)
            return;

        DeleteClicked?.Invoke(SlotIndex);
    }
}
