using CrystalMagic.Core;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class CharacterUI_SkillItemView : UISubView<CharacterUI_SkillItemData>, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private CrystalMagic.UI.CharacterSkillDisplayData _data;

    public event Action<CrystalMagic.UI.CharacterSkillDisplayData, PointerEventData> DragStarted;
    public event Action<CrystalMagic.UI.CharacterSkillDisplayData, PointerEventData> Dragging;
    public event Action<CrystalMagic.UI.CharacterSkillDisplayData, PointerEventData> DragEnded;

    public void Render(CrystalMagic.UI.CharacterSkillDisplayData data)
    {
        Rebind();
        _data = data;

        if (data == null)
        {
            UI.Skill.Image.sprite = null;
            UI.Effect_EffectIcon.Image.sprite = null;
            UI.Index_IndexNum.TextMeshProUGUI.text = string.Empty;
            return;
        }

        UI.Index_IndexNum.TextMeshProUGUI.text = data.DisplayIndex.ToString();
        UI.Skill.Image.sprite = LoadIcon(data.SkillIconPath);
        UI.Effect_EffectIcon.Image.sprite = LoadIcon(data.EffectIconPath);
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

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<Sprite>(iconPath);
    }
}
