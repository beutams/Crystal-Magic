using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.UI;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TrainingDebugUI : UIBase<TrainingDebugUIData, TrainingDebugUIModel>
{
    private readonly List<TrainingDebugUI_UnitItemView> _unitItemViews = new();

    public event Action ToggleRequested;
    public event Action SpawnRequested;
    public event Action UnitControlRequested;
    public event Action BackRequested;
    public event Action<EntitySelection> UnitSelected;
    public event Action ClearAIRequested;
    public event Action ClearStateTransitionsRequested;
    public event Action<string> FacingRequested;
    public event Action CastSkillRequested;
    public event Action ForceStateRequested;

    protected override void OnInit()
    {
        base.OnInit();
        EnsureUnitControlWidgets();
    }

    public override void OnOpen()
    {
        EnsureUnitControlWidgets();

        UI.ToggleButton.ButtonPlus.onClick.AddListener(OnToggleClicked);
        UI.SpawnButton.ButtonPlus.onClick.AddListener(OnSpawnClicked);
        UI.UnitControlButton.ButtonPlus.onClick.AddListener(OnUnitControlClicked);
        UI.BackButton.ButtonPlus.onClick.AddListener(OnBackClicked);
        UI.ClearAIButton.ButtonPlus.onClick.AddListener(OnClearAIButtonClicked);
        UI.ClearTransitionsButton.ButtonPlus.onClick.AddListener(OnClearTransitionsButtonClicked);
        AddButtonListener(UI.FaceLeftButton, OnFaceLeftButtonClicked);
        AddButtonListener(UI.FaceRightButton, OnFaceRightButtonClicked);
        AddButtonListener(UI.FaceUpButton, OnFaceUpButtonClicked);
        AddButtonListener(UI.FaceDownButton, OnFaceDownButtonClicked);
        AddButtonListener(UI.CastSkillButton, OnCastSkillButtonClicked);
        UI.ForceStateButton.ButtonPlus.onClick.AddListener(OnForceStateButtonClicked);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.ToggleButton.ButtonPlus.onClick.RemoveListener(OnToggleClicked);
        UI.SpawnButton.ButtonPlus.onClick.RemoveListener(OnSpawnClicked);
        UI.UnitControlButton.ButtonPlus.onClick.RemoveListener(OnUnitControlClicked);
        UI.BackButton.ButtonPlus.onClick.RemoveListener(OnBackClicked);
        UI.ClearAIButton.ButtonPlus.onClick.RemoveListener(OnClearAIButtonClicked);
        UI.ClearTransitionsButton.ButtonPlus.onClick.RemoveListener(OnClearTransitionsButtonClicked);
        RemoveButtonListener(UI.FaceLeftButton, OnFaceLeftButtonClicked);
        RemoveButtonListener(UI.FaceRightButton, OnFaceRightButtonClicked);
        RemoveButtonListener(UI.FaceUpButton, OnFaceUpButtonClicked);
        RemoveButtonListener(UI.FaceDownButton, OnFaceDownButtonClicked);
        RemoveButtonListener(UI.CastSkillButton, OnCastSkillButtonClicked);
        UI.ForceStateButton.ButtonPlus.onClick.RemoveListener(OnForceStateButtonClicked);
        UISubViewBase.ReleaseAllToPool(_unitItemViews);
        base.OnClose();
    }

    public string GetUnitName()
    {
        TMP_InputField input = UI.UnitNameInput.TMP_InputField;
        return input != null ? input.text.Replace("\u200B", string.Empty).Replace("\uFEFF", string.Empty).Trim() : string.Empty;
    }

    public string GetStateName()
    {
        TMP_InputField input = UI.StateNameInput.TMP_InputField;
        return input != null ? input.text : string.Empty;
    }

    public string GetSkillId()
    {
        TMP_InputField input = UI.SkillIdInput.TMP_InputField;
        return input != null ? input.text : string.Empty;
    }

    protected override void RefreshView()
    {
        UI.DebugPanel.GameObject.SetActive(Model.IsExpanded);
        UI.CommandPage.GameObject.SetActive(Model.IsExpanded && !Model.IsUnitControlOpen);
        UI.UnitControlPage.GameObject.SetActive(Model.IsExpanded && Model.IsUnitControlOpen);
        UI.ResultText.TextMeshProUGUI.text = Model.ResultMessage;
        UI.InspectorText.TextMeshProUGUI.text = Model.InspectorText;

        if (Model.IsUnitControlOpen)
            RenderUnits(Model.Units);
    }

    private void RenderUnits(IReadOnlyList<TrainingDebugUnitSnapshot> units)
    {
        UI.UnitListContent_UnitItem.GameObject.SetActive(false);

        int count = units != null ? units.Count : 0;
        while (_unitItemViews.Count > count)
        {
            int lastIndex = _unitItemViews.Count - 1;
            UISubViewBase.ReleaseToPool(_unitItemViews[lastIndex]);
            _unitItemViews.RemoveAt(lastIndex);
        }

        TrainingDebugUI_UnitItemView template = UI.UnitListContent_UnitItem.GameObject.GetComponent<TrainingDebugUI_UnitItemView>();
        UISubViewBase.EnsurePoolCapacity(template, Math.Max(1, count), Math.Max(1, count));
        while (_unitItemViews.Count < count)
        {
            TrainingDebugUI_UnitItemView item = UISubViewBase.AcquireFromPool(template, UI.UnitListContent.GameObject.transform);
            item.Selected -= OnUnitItemSelected;
            item.Selected += OnUnitItemSelected;
            _unitItemViews.Add(item);
        }

        for (int i = 0; i < _unitItemViews.Count; i++)
            _unitItemViews[i].Render(units[i]);
    }

    private void OnToggleClicked() => ToggleRequested?.Invoke();
    private void OnSpawnClicked() => SpawnRequested?.Invoke();
    private void OnUnitControlClicked() => UnitControlRequested?.Invoke();
    private void OnBackClicked() => BackRequested?.Invoke();
    private void OnClearAIButtonClicked() => ClearAIRequested?.Invoke();
    private void OnClearTransitionsButtonClicked() => ClearStateTransitionsRequested?.Invoke();
    private void OnFaceLeftButtonClicked() => FacingRequested?.Invoke("Left");
    private void OnFaceRightButtonClicked() => FacingRequested?.Invoke("Right");
    private void OnFaceUpButtonClicked() => FacingRequested?.Invoke("Up");
    private void OnFaceDownButtonClicked() => FacingRequested?.Invoke("Down");
    private void OnCastSkillButtonClicked() => CastSkillRequested?.Invoke();
    private void OnForceStateButtonClicked() => ForceStateRequested?.Invoke();
    private void OnUnitItemSelected(EntitySelection selection) => UnitSelected?.Invoke(selection);

    private void EnsureUnitControlWidgets()
    {
        RectTransform panel = UI.DebugPanel.RectTransform;
        panel.sizeDelta = new Vector2(590f, 590f);

        SetRightControlRect(UI.InspectorText.RectTransform, 0.62f, -8f, -56f);
        SetRightControlRect(UI.ClearAIButton.RectTransform, 1f, -214f, 38f);
        SetRightControlRect(UI.ClearTransitionsButton.RectTransform, 1f, -260f, 38f);
        SetRightControlRect(UI.StateNameInput.RectTransform, 1f, -398f, 38f);
        SetRightControlRect(UI.ForceStateButton.RectTransform, 1f, -444f, 38f);

        Transform parent = UI.UnitControlPage.GameObject.transform;
        UI.FaceLeftButton = UINode.From(EnsureButton(UI.FaceLeftButton, parent, "FaceLeftButton", "Left", new Vector2(0.46f, 1f), new Vector2(0.587f, 1f), -170f));
        UI.FaceRightButton = UINode.From(EnsureButton(UI.FaceRightButton, parent, "FaceRightButton", "Right", new Vector2(0.591f, 1f), new Vector2(0.718f, 1f), -170f));
        UI.FaceUpButton = UINode.From(EnsureButton(UI.FaceUpButton, parent, "FaceUpButton", "Up", new Vector2(0.722f, 1f), new Vector2(0.849f, 1f), -170f));
        UI.FaceDownButton = UINode.From(EnsureButton(UI.FaceDownButton, parent, "FaceDownButton", "Down", new Vector2(0.853f, 1f), new Vector2(1f, 1f), -170f));
        UI.SkillIdInput = UINode.From(EnsureInput(UI.SkillIdInput, parent, "SkillIdInput", "0", -306f));
        UI.CastSkillButton = UINode.From(EnsureButton(UI.CastSkillButton, parent, "CastSkillButton", "Cast Skill", new Vector2(0.46f, 1f), Vector2.one, -352f));
    }

    private static void AddButtonListener(UINode node, UnityAction listener)
    {
        ButtonPlus button = node?.ButtonPlus;
        if (button == null)
            return;

        button.onClick ??= new UnityEvent();
        button.onClick.AddListener(listener);
    }

    private static void RemoveButtonListener(UINode node, UnityAction listener)
    {
        ButtonPlus button = node?.ButtonPlus;
        if (button?.onClick != null)
            button.onClick.RemoveListener(listener);
    }

    private static void SetRightControlRect(RectTransform rectTransform, float anchorMinY, float topOffset, float height)
    {
        rectTransform.anchorMin = new Vector2(0.46f, anchorMinY);
        rectTransform.anchorMax = Vector2.one;
        rectTransform.anchoredPosition = new Vector2(-8f, topOffset);
        rectTransform.sizeDelta = new Vector2(-8f, height);
        rectTransform.pivot = new Vector2(0.5f, 1f);
    }

    private static GameObject CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, float topOffset)
    {
        GameObject buttonObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ButtonPlus));
        buttonObject.layer = LayerMask.NameToLayer("UI");
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.17f, 0.29f, 0.39f, 1f);
        image.type = Image.Type.Sliced;
        buttonObject.GetComponent<ButtonPlus>().onClick = new UnityEvent();
        SetControlRect(buttonObject.GetComponent<RectTransform>(), anchorMin, anchorMax, topOffset, 34f);

        TextMeshProUGUI text = CreateText("Label", buttonObject.transform, label, 16f, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, new Vector2(-8f, -2f));
        return buttonObject;
    }

    private static GameObject EnsureButton(UINode node, Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, float topOffset)
    {
        GameObject buttonObject = node?.GameObject;
        if (buttonObject == null)
            buttonObject = parent.Find(name)?.gameObject;

        if (buttonObject == null)
            return CreateButton(parent, name, label, anchorMin, anchorMax, topOffset);

        buttonObject.layer = LayerMask.NameToLayer("UI");
        if (buttonObject.GetComponent<CanvasRenderer>() == null)
            buttonObject.AddComponent<CanvasRenderer>();

        Image image = buttonObject.GetComponent<Image>() ?? buttonObject.AddComponent<Image>();
        ButtonPlus button = buttonObject.GetComponent<ButtonPlus>() ?? buttonObject.AddComponent<ButtonPlus>();
        button.onClick ??= new UnityEvent();

        image.color = new Color(0.17f, 0.29f, 0.39f, 1f);
        image.type = Image.Type.Sliced;
        SetControlRect(buttonObject.GetComponent<RectTransform>(), anchorMin, anchorMax, topOffset, 34f);

        TextMeshProUGUI text = buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text == null)
            text = CreateText("Label", buttonObject.transform, label, 16f, TextAlignmentOptions.Center);
        else
        {
            text.text = label;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 16f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        Stretch(text.rectTransform, new Vector2(-8f, -2f));
        return buttonObject;
    }

    private static GameObject CreateInput(Transform parent, string name, string value, float topOffset)
    {
        GameObject inputObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        inputObject.layer = LayerMask.NameToLayer("UI");
        inputObject.transform.SetParent(parent, false);

        Image image = inputObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.16f, 0.18f, 1f);
        image.type = Image.Type.Sliced;
        SetControlRect(inputObject.GetComponent<RectTransform>(), new Vector2(0.46f, 1f), Vector2.one, topOffset, 38f);

        GameObject area = new("Text Area", typeof(RectTransform));
        area.layer = LayerMask.NameToLayer("UI");
        area.transform.SetParent(inputObject.transform, false);
        Stretch(area.GetComponent<RectTransform>(), new Vector2(-18f, -8f));

        TextMeshProUGUI text = CreateText("Text", area.transform, value, 20f, TextAlignmentOptions.MidlineLeft);
        Stretch(text.rectTransform);

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.textViewport = area.GetComponent<RectTransform>();
        input.textComponent = text;
        input.characterLimit = 16;
        input.text = value;
        return inputObject;
    }

    private static GameObject EnsureInput(UINode node, Transform parent, string name, string value, float topOffset)
    {
        GameObject inputObject = node?.GameObject;
        if (inputObject == null)
            inputObject = parent.Find(name)?.gameObject;

        if (inputObject == null)
            return CreateInput(parent, name, value, topOffset);

        inputObject.layer = LayerMask.NameToLayer("UI");
        if (inputObject.GetComponent<CanvasRenderer>() == null)
            inputObject.AddComponent<CanvasRenderer>();

        Image image = inputObject.GetComponent<Image>() ?? inputObject.AddComponent<Image>();
        TMP_InputField input = inputObject.GetComponent<TMP_InputField>() ?? inputObject.AddComponent<TMP_InputField>();
        image.color = new Color(0.12f, 0.16f, 0.18f, 1f);
        image.type = Image.Type.Sliced;
        SetControlRect(inputObject.GetComponent<RectTransform>(), new Vector2(0.46f, 1f), Vector2.one, topOffset, 38f);

        RectTransform area = inputObject.transform.Find("Text Area") as RectTransform;
        if (area == null)
        {
            GameObject areaObject = new("Text Area", typeof(RectTransform));
            areaObject.layer = LayerMask.NameToLayer("UI");
            areaObject.transform.SetParent(inputObject.transform, false);
            area = areaObject.GetComponent<RectTransform>();
        }

        Stretch(area, new Vector2(-18f, -8f));
        TextMeshProUGUI text = area.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text == null)
            text = CreateText("Text", area, value, 20f, TextAlignmentOptions.MidlineLeft);
        else
        {
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 20f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        Stretch(text.rectTransform);
        input.targetGraphic = image;
        input.textViewport = area;
        input.textComponent = text;
        input.characterLimit = 16;
        if (string.IsNullOrEmpty(input.text))
            input.text = value;
        return inputObject;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = LayerMask.NameToLayer("UI");
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void SetControlRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, float topOffset, float height)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.anchoredPosition = new Vector2(-4f, topOffset);
        rectTransform.sizeDelta = new Vector2(-4f, height);
        rectTransform.pivot = new Vector2(0.5f, 1f);
    }

    private static void Stretch(RectTransform rectTransform, Vector2 sizeDelta = default)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }
}

public readonly struct EntitySelection
{
    public EntitySelection(int index, int version)
    {
        Index = index;
        Version = version;
    }

    public int Index { get; }
    public int Version { get; }
}
