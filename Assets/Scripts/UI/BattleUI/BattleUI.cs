using System.Collections.Generic;
using System;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;

public class BattleUI : UIBase<BattleUIData, BattleUIModel>
{
    private readonly List<BattleUI_SkillItemView> _skillItemViews = new();
    private float _hpMaskBaseWidth = -1f;
    private float _mpMaskBaseWidth = -1f;
    private float _chantMaskBaseWidth = -1f;

    public event Action<int> PropShortcutUseRequested;
    public event Action<int, int> PropShortcutBindRequested;

    public override void OnOpen()
    {
        EnsureSkillItemTemplateView();
        CacheBarWidths();
        UI.Bar.GameObject.SetActive(false);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UISubViewBase.ReleaseAllToPool(_skillItemViews);
        base.OnClose();
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        Model?.RefreshRuntime();
    }

    protected override void RefreshView()
    {
        if (Model == null)
            return;

        RenderSkillChain(Model.SkillItems);
        RenderChantProgress(Model.IsChanting, Model.ChantProgress);
        RenderVitalityAndMana(Model.HpRatio, Model.MpRatio, Model.CurrentHp, Model.CurrentMp);
    }

    private void RenderSkillChain(IReadOnlyList<BattleSkillDisplayData> skillItems)
    {
        int skillItemCount = skillItems != null ? skillItems.Count : 0;
        EnsureSkillItemViews(skillItemCount);

        for (int i = 0; i < _skillItemViews.Count; i++)
        {
            BattleSkillDisplayData data = skillItems != null && i < skillItems.Count ? skillItems[i] : null;
            _skillItemViews[i].Render(data);
        }
    }

    private void EnsureSkillItemViews(int itemCount)
    {
        UI.SkillChain_Viewport_Content_SkillItem.GameObject.SetActive(false);

        while (_skillItemViews.Count > itemCount)
        {
            int lastIndex = _skillItemViews.Count - 1;
            BattleUI_SkillItemView itemView = _skillItemViews[lastIndex];
            UISubViewBase.ReleaseToPool(itemView);
            _skillItemViews.RemoveAt(lastIndex);
        }

        BattleUI_SkillItemView templateView = UI.SkillChain_Viewport_Content_SkillItem.GameObject.GetComponent<BattleUI_SkillItemView>();
        UISubViewBase.EnsurePoolCapacity(templateView, itemCount, itemCount);

        while (_skillItemViews.Count < itemCount)
        {
            BattleUI_SkillItemView itemView = UISubViewBase.AcquireFromPool(templateView, UI.SkillChain_Viewport_Content.GameObject.transform);
            _skillItemViews.Add(itemView);
        }
    }

    private void EnsureSkillItemTemplateView()
    {
        if (UI.SkillChain_Viewport_Content_SkillItem.GameObject.GetComponent<BattleUI_SkillItemView>() == null)
            UI.SkillChain_Viewport_Content_SkillItem.GameObject.AddComponent<BattleUI_SkillItemView>();
    }

    private void RenderChantProgress(bool isChanting, float progress)
    {
        UI.Bar.GameObject.SetActive(isChanting);
        SetMaskWidth(UI.Bar_BarMask.RectTransform, progress, _chantMaskBaseWidth);
    }

    private void RenderVitalityAndMana(float hpRatio, float mpRatio, float currentHp, float currentMp)
    {
        CacheBarWidths();
        SetMaskWidth(UI.HP_BarMask.RectTransform, hpRatio, _hpMaskBaseWidth);
        SetMaskWidth(UI.MP_BarMask.RectTransform, mpRatio, _mpMaskBaseWidth);
        SetValueText(UI.HP_Value.TextMeshProUGUI, currentHp);
        SetValueText(UI.MP_Value.TextMeshProUGUI, currentMp);
    }

    private void CacheBarWidths()
    {
        if (_hpMaskBaseWidth <= 0f)
        {
            _hpMaskBaseWidth = UI.HP_BarMask.RectTransform.rect.width;
            if (_hpMaskBaseWidth <= 0f)
                _hpMaskBaseWidth = UI.HP_BarMask.RectTransform.sizeDelta.x;
        }

        if (_mpMaskBaseWidth <= 0f)
        {
            _mpMaskBaseWidth = UI.MP_BarMask.RectTransform.rect.width;
            if (_mpMaskBaseWidth <= 0f)
                _mpMaskBaseWidth = UI.MP_BarMask.RectTransform.sizeDelta.x;
        }

        if (_chantMaskBaseWidth <= 0f)
        {
            _chantMaskBaseWidth = UI.Bar_BarMask.RectTransform.rect.width;
            if (_chantMaskBaseWidth <= 0f)
                _chantMaskBaseWidth = UI.Bar_BarMask.RectTransform.sizeDelta.x;
        }
    }

    private static void SetMaskWidth(RectTransform rectTransform, float ratio, float baseWidth)
    {
        if (baseWidth <= 0f)
            return;

        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, baseWidth * Mathf.Clamp01(ratio));
    }

    private static void SetValueText(TMPro.TextMeshProUGUI text, float current)
    {
        text.text = FormatValue(current);
    }

    private static string FormatValue(float value)
    {
        return Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.#");
    }

    public void RequestPropShortcutUse(int shortcutIndex)
    {
        PropShortcutUseRequested?.Invoke(shortcutIndex);
    }

    public void RequestPropShortcutBind(int propSlotIndex, int shortcutIndex)
    {
        PropShortcutBindRequested?.Invoke(propSlotIndex, shortcutIndex);
    }
}

public class BattleUI_SkillItemView : UISubView<BattleUI_SkillItemData>
{
    public void Render(BattleSkillDisplayData data)
    {
        Rebind();

        if (data == null)
        {
            UI.SkillMask_Skill.Image.sprite = null;
            UI.Effect_EffectIcon.Image.sprite = null;
            UI.Effect.GameObject.SetActive(false);
            UI.Effect_EffectIcon.GameObject.SetActive(false);
            UI.IndexNum.TextMeshProUGUI.text = string.Empty;
            UI.Select.GameObject.SetActive(false);
            return;
        }

        UI.SkillMask_Skill.Image.sprite = LoadIcon(data.SkillIconPath);
        UI.Effect.GameObject.SetActive(data.CanShowAddition);

        Sprite additionIcon = LoadIcon(data.AdditionIconPath);
        UI.Effect_EffectIcon.Image.sprite = additionIcon;
        UI.Effect_EffectIcon.GameObject.SetActive(data.CanShowAddition && additionIcon != null);
        UI.IndexNum.TextMeshProUGUI.text = data.DisplayIndex.ToString();
        UI.Select.GameObject.SetActive(data.IsSelected);
    }

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return LoadManagedSprite(iconPath);
    }
}

public class BattleUI_SkillItemData : UIData
{
    public UINode Background;
    public UINode SkillMask;
    public UINode SkillMask_Skill;
    public UINode Effect;
    public UINode Effect_EffectIcon;
    public UINode IndexNum;
    public UINode Select;

    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        SkillMask = UINode.From(Find(root, "SkillMask"));
        SkillMask_Skill = UINode.From(Find(root, "SkillMask/Skill"));
        Effect = UINode.From(Find(root, "Effect"));
        Effect_EffectIcon = UINode.From(Find(root, "Effect/EffectIcon"));
        IndexNum = UINode.From(Find(root, "IndexNum"));
        Select = UINode.From(Find(root, "Select"));
    }
}
