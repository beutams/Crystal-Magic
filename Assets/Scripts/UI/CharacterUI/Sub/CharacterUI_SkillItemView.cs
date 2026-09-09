using CrystalMagic.Core;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class CharacterUI_SkillItemView : UISubView<CharacterUI_SkillItemData>, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private CrystalMagic.UI.CharacterSkillDisplayData _data;

    public event Action<CrystalMagic.UI.CharacterSkillDisplayData> AdditionClicked;
    public event Action<CrystalMagic.UI.CharacterSkillDisplayData, PointerEventData> DragStarted;
    public event Action<CrystalMagic.UI.CharacterSkillDisplayData, PointerEventData> Dragging;
    public event Action<CrystalMagic.UI.CharacterSkillDisplayData, PointerEventData> DragEnded;

    public void Render(CrystalMagic.UI.CharacterSkillDisplayData data)
    {
        Rebind();
        _data = data;

        if (data == null)
        {
            UI.SkillMask_Skill.Image.sprite = null;
            UI.Effect_EffectIcon.Image.sprite = null;
            UI.Effect.GameObject.SetActive(false);
            UI.Effect_EffectIcon.GameObject.SetActive(false);
            UI.IndexNum.TextMeshProUGUI.text = string.Empty;
            return;
        }

        UI.IndexNum.TextMeshProUGUI.text = data.DisplayIndex.ToString();
        UI.SkillMask_Skill.Image.sprite = LoadIcon(data.SkillIconPath);
        UI.Effect.GameObject.SetActive(data.CanSelectAddition);

        Sprite additionIcon = LoadIcon(data.AdditionIconPath);
        UI.Effect_EffectIcon.Image.sprite = additionIcon;
        UI.Effect_EffectIcon.GameObject.SetActive(data.CanSelectAddition && additionIcon != null);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_data == null || !_data.CanSelectAddition || eventData == null)
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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_data == null || eventData == null)
            return;

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (!RectTransformUtility.RectangleContainsScreenPoint(UI.Effect.RectTransform, eventData.position, eventCamera))
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
