using CrystalMagic.Core;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class CharacterUI_SkillItemView : UISubView<CharacterUI_SkillItemData>, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    private CrystalMagic.UI.CharacterSkillDisplayData _data;

    public event Action<CrystalMagic.UI.CharacterSkillDisplayData> AdditionClicked;
    public event Action<CrystalMagic.UI.CharacterSkillDisplayData, PointerEventData> DragStarted;
    public event Action<CrystalMagic.UI.CharacterSkillDisplayData, PointerEventData> Dragging;
    public event Action<CrystalMagic.UI.CharacterSkillDisplayData, PointerEventData> DragEnded;

    protected override void Awake()
    {
        base.Awake();
        UI.Effect.ButtonPlus.onClick.RemoveListener(HandleAdditionClicked);
        UI.Effect.ButtonPlus.onClick.AddListener(HandleAdditionClicked);
    }

    public void OnPointerEnter(PointerEventData eventData) => UI.Select.GameObject.SetActive(true);
    public void OnPointerExit(PointerEventData eventData) => UI.Select.GameObject.SetActive(false);
    private void OnDisable() => UI.Select.GameObject.SetActive(false);

    public void Render(CrystalMagic.UI.CharacterSkillDisplayData data)
    {
        Rebind();
        _data = data;

        if (data == null)
        {
            UI.SkillMask_Skill.Image.sprite = null;
            UI.SkillMask_Skill.GameObject.SetActive(false);
            UI.Effect_EffectIcon.Image.sprite = null;
            UI.Effect.GameObject.SetActive(false);
            UI.Effect_EffectIcon.GameObject.SetActive(false);
            UI.IndexNum.TextMeshProUGUI.text = string.Empty;
            UI.NameLabel.TextMeshProUGUI.text = string.Empty;
            UI.StatsLabel.TextMeshProUGUI.text = string.Empty;
            UI.Connector.GameObject.SetActive(false);
            return;
        }

        UI.IndexNum.TextMeshProUGUI.text = data.DisplayIndex.ToString();
        UI.NameLabel.TextMeshProUGUI.text = data.Name;
        UI.StatsLabel.TextMeshProUGUI.text = $"{data.MpCost} MP · {data.ChantDuration:0.##} s";
        UI.Connector.GameObject.SetActive(data.SkillIndex > 0);
        UI.SkillMask_Skill.Image.sprite = LoadIcon(data.SkillIconPath);
        UI.SkillMask_Skill.GameObject.SetActive(UI.SkillMask_Skill.Image.sprite != null);
        UI.Effect.GameObject.SetActive(data.CanSelectAddition);

        Sprite additionIcon = LoadIcon(data.AdditionIconPath);
        UI.Effect_EffectIcon.Image.sprite = additionIcon;
        UI.Effect_EffectIcon.GameObject.SetActive(data.CanSelectAddition && additionIcon != null);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_data == null || eventData == null)
            return;

        DragStarted?.Invoke(_data, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_data == null || eventData == null)
            return;

        Dragging?.Invoke(_data, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_data == null || eventData == null)
            return;

        DragEnded?.Invoke(_data, eventData);
    }

    private void HandleAdditionClicked()
    {
        if (_data == null || !_data.CanSelectAddition)
            return;
        AdditionClicked?.Invoke(_data);
    }

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return LoadManagedSprite(iconPath);
    }
}
