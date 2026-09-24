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
    private readonly BattleUI_PropSlotClickHandler[] _propSlotHandlers = new BattleUI_PropSlotClickHandler[3];

    public event Action<int> PropShortcutUseRequested;

    public override void OnOpen()
    {
        EnsureSkillItemTemplateView();
        BindPropSlotHandlers();
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
        RenderPropShortcuts(Model.PropShortcutItems);
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

    private void RenderPropShortcuts(IReadOnlyList<BattlePropShortcutDisplayData> items)
    {
        RenderPropShortcut(
            UI.PropShortcuts_PropSlot1_Icon,
            UI.PropShortcuts_PropSlot1_Count,
            UI.PropShortcuts_PropSlot1_Key,
            UI.PropShortcuts_PropSlot1_Cooldown,
            items != null && items.Count > 0 ? items[0] : null,
            "Z");
        RenderPropShortcut(
            UI.PropShortcuts_PropSlot2_Icon,
            UI.PropShortcuts_PropSlot2_Count,
            UI.PropShortcuts_PropSlot2_Key,
            UI.PropShortcuts_PropSlot2_Cooldown,
            items != null && items.Count > 1 ? items[1] : null,
            "X");
        RenderPropShortcut(
            UI.PropShortcuts_PropSlot3_Icon,
            UI.PropShortcuts_PropSlot3_Count,
            UI.PropShortcuts_PropSlot3_Key,
            UI.PropShortcuts_PropSlot3_Cooldown,
            items != null && items.Count > 2 ? items[2] : null,
            "C");
    }

    private void RenderPropShortcut(
        UINode iconNode,
        UINode countNode,
        UINode keyNode,
        UINode cooldownNode,
        BattlePropShortcutDisplayData data,
        string key)
    {
        iconNode.Image.sprite = LoadPropIcon(data != null ? data.IconPath : string.Empty);
        iconNode.Image.color = data != null && data.ItemId >= 0
            ? Color.white
            : new Color(1f, 1f, 1f, 0.2f);
        countNode.TextMeshProUGUI.text = data != null && data.Count > 0 ? data.Count.ToString() : string.Empty;
        keyNode.TextMeshProUGUI.text = key;

        float remainingRatio = data != null ? Mathf.Clamp01(data.CooldownRatio) : 0f;
        cooldownNode.GameObject.SetActive(remainingRatio > 0f);
        RectTransform cooldown = cooldownNode.RectTransform;
        Vector2 anchorMin = cooldown.anchorMin;
        anchorMin.y = 1f - remainingRatio;
        cooldown.anchorMin = anchorMin;
        cooldown.offsetMin = Vector2.zero;
        cooldown.offsetMax = Vector2.zero;
    }

    private void BindPropSlotHandlers()
    {
        BindPropSlotHandler(0, UI.PropShortcuts_PropSlot1.GameObject);
        BindPropSlotHandler(1, UI.PropShortcuts_PropSlot2.GameObject);
        BindPropSlotHandler(2, UI.PropShortcuts_PropSlot3.GameObject);
    }

    private void BindPropSlotHandler(int slotIndex, GameObject gameObject)
    {
        BattleUI_PropSlotClickHandler handler = gameObject.GetComponent<BattleUI_PropSlotClickHandler>();
        handler.Initialize(slotIndex);
        handler.Clicked -= RequestPropShortcutUse;
        handler.Clicked += RequestPropShortcutUse;
        _propSlotHandlers[slotIndex] = handler;
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

    private Sprite LoadPropIcon(string iconPath)
    {
        return string.IsNullOrEmpty(iconPath) ? null : LoadManagedSprite(iconPath);
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
