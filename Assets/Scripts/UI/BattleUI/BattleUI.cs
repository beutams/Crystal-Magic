using System.Collections.Generic;
using System;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
public class BattleUI : UIBase<BattleUIData, BattleUIModel>
{
    [SerializeField, Min(0f)] private float _chantFadeDuration = 0.2f;

    private readonly List<BattleUI_SkillItemView> _skillItemViews = new();
    private readonly List<BattleUI_BuffItemView> _buffItemViews = new();
    private float _hpMaskBaseWidth = -1f;
    private float _mpMaskBaseWidth = -1f;
    private float _chantMaskBaseWidth = -1f;
    private float _chantBarHorizontalPadding;
    private float _displayedChantProgress;
    private float _skillChainVisibleAlpha;
    private float _chantBarVisibleAlpha;
    private float _chantVisibility;
    private bool _showChantUI;
    private readonly BattleUI_PropSlotClickHandler[] _propSlotHandlers = new BattleUI_PropSlotClickHandler[3];

    public event Action<int> PropShortcutUseRequested;

    protected override void OnInit()
    {
        base.OnInit();
        _skillChainVisibleAlpha = UI.SkillChain.CanvasGroup.alpha;
        _chantBarVisibleAlpha = UI.Bar.CanvasGroup.alpha;
        _chantBarHorizontalPadding = Mathf.Max(0f,
            UI.Bar_Border.RectTransform.rect.width - UI.Bar_BarMask_Bar.RectTransform.rect.width);
        ResetChantVisibility();
    }

    public override void OnOpen()
    {
        EnsureSkillItemTemplateView();
        EnsureBuffItemTemplateView();
        BindPropSlotHandlers();
        CacheBarWidths();
        // Keep layout active so ContentSizeFitter can size the fixed skill chain while hidden.
        UI.SkillChain.GameObject.SetActive(true);
        UI.Bar.GameObject.SetActive(true);
        ResetChantVisibility();
        base.OnOpen();
    }

    public override void OnClose()
    {
        ResetChantVisibility();
        UISubViewBase.ReleaseAllToPool(_skillItemViews);
        UISubViewBase.ReleaseAllToPool(_buffItemViews);
        base.OnClose();
    }

    private void LateUpdate()
    {
        Model?.RefreshRuntime();
        SyncChantBarWidth();
        UpdateChantVisibility(Time.unscaledDeltaTime);
    }

    protected override void RefreshView()
    {
        if (Model == null)
            return;

        RenderSkillChain(Model.SkillItems);
        RenderChantProgress(Model.IsChanting, Model.ChantProgress);
        RenderVitalityAndMana(Model.HpRatio, Model.MpRatio, Model.CurrentHp, Model.CurrentMp);
        RenderBuffs(Model.BuffItems);
        RenderPropShortcuts(Model.PropShortcutItems);
    }

    private void RenderSkillChain(IReadOnlyList<BattleSkillDisplayData> skillItems)
    {
        int skillItemCount = skillItems != null ? skillItems.Count : 0;
        bool layoutChanged = _skillItemViews.Count != skillItemCount;
        EnsureSkillItemViews(skillItemCount);

        for (int i = 0; i < _skillItemViews.Count; i++)
        {
            BattleSkillDisplayData data = skillItems != null && i < skillItems.Count ? skillItems[i] : null;
            _skillItemViews[i].Render(data);
        }

        if (layoutChanged)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(UI.SkillChain.RectTransform);
            SyncChantBarWidth();
        }
    }

    private void EnsureSkillItemViews(int itemCount)
    {
        UI.SkillChain_SkillItem.GameObject.SetActive(false);

        while (_skillItemViews.Count > itemCount)
        {
            int lastIndex = _skillItemViews.Count - 1;
            BattleUI_SkillItemView itemView = _skillItemViews[lastIndex];
            UISubViewBase.ReleaseToPool(itemView);
            _skillItemViews.RemoveAt(lastIndex);
        }

        BattleUI_SkillItemView templateView = UI.SkillChain_SkillItem.GameObject.GetComponent<BattleUI_SkillItemView>();
        UISubViewBase.EnsurePoolCapacity(templateView, itemCount, itemCount);

        while (_skillItemViews.Count < itemCount)
        {
            BattleUI_SkillItemView itemView = UISubViewBase.AcquireFromPool(templateView, UI.SkillChain.GameObject.transform);
            _skillItemViews.Add(itemView);
        }
    }

    private void EnsureSkillItemTemplateView()
    {
        if (UI.SkillChain_SkillItem.GameObject.GetComponent<BattleUI_SkillItemView>() == null)
            UI.SkillChain_SkillItem.GameObject.AddComponent<BattleUI_SkillItemView>();
    }

    private void RenderBuffs(IReadOnlyList<UnitHealthBarBuffDisplayData> buffs)
    {
        int itemCount = buffs?.Count ?? 0;
        UI.HP_BuffRoot.GameObject.SetActive(itemCount > 0);
        UI.HP_BuffRoot_BuffIcon.GameObject.SetActive(false);
        EnsureBuffItemViews(itemCount);

        for (int i = 0; i < _buffItemViews.Count; i++)
            _buffItemViews[i].Render(buffs[i]);
    }

    private void EnsureBuffItemViews(int itemCount)
    {
        while (_buffItemViews.Count > itemCount)
        {
            int lastIndex = _buffItemViews.Count - 1;
            BattleUI_BuffItemView itemView = _buffItemViews[lastIndex];
            UISubViewBase.ReleaseToPool(itemView);
            _buffItemViews.RemoveAt(lastIndex);
        }

        BattleUI_BuffItemView templateView =
            UI.HP_BuffRoot_BuffIcon.GameObject.GetComponent<BattleUI_BuffItemView>();
        UISubViewBase.EnsurePoolCapacity(templateView, itemCount, itemCount);

        while (_buffItemViews.Count < itemCount)
        {
            BattleUI_BuffItemView itemView = UISubViewBase.AcquireFromPool(
                templateView,
                UI.HP_BuffRoot.GameObject.transform);
            _buffItemViews.Add(itemView);
        }
    }

    private void EnsureBuffItemTemplateView()
    {
        if (UI.HP_BuffRoot_BuffIcon.GameObject.GetComponent<BattleUI_BuffItemView>() == null)
            UI.HP_BuffRoot_BuffIcon.GameObject.AddComponent<BattleUI_BuffItemView>();
    }

    private void RenderChantProgress(bool isChanting, float progress)
    {
        _showChantUI = isChanting;
        // Retain the last progress during fade-out instead of snapping the bar empty.
        if (isChanting)
        {
            _displayedChantProgress = Mathf.Clamp01(progress);
            SetMaskWidth(UI.Bar_BarMask.RectTransform, _displayedChantProgress, _chantMaskBaseWidth);
        }
    }

    private void SyncChantBarWidth()
    {
        RectTransform background = UI.SkillChain_BG.RectTransform;
        RectTransform bar = UI.Bar.RectTransform;
        // Convert between the two local spaces to include SkillChain's authored scale.
        Vector3 widthVector = background.TransformVector(new Vector3(background.rect.width, 0f, 0f));
        float outerWidth = bar.InverseTransformVector(widthVector).magnitude;
        float innerWidth = Mathf.Max(0f, outerWidth - _chantBarHorizontalPadding);
        if (outerWidth <= 0f || Mathf.Approximately(_chantMaskBaseWidth, innerWidth))
            return;

        _chantMaskBaseWidth = innerWidth;
        bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, innerWidth);
        UI.Bar_Border.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, outerWidth);
        UI.Bar_BarMask_Bar.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, innerWidth);
        SetMaskWidth(UI.Bar_BarMask.RectTransform, _displayedChantProgress, innerWidth);
    }

    private void UpdateChantVisibility(float deltaTime)
    {
        float target = _showChantUI ? 1f : 0f;
        _chantVisibility = _chantFadeDuration > 0f
            ? Mathf.MoveTowards(_chantVisibility, target, Mathf.Max(0f, deltaTime) / _chantFadeDuration)
            : target;
        ApplyChantVisibility();
    }

    private void ResetChantVisibility()
    {
        _showChantUI = false;
        _chantVisibility = 0f;
        ApplyChantVisibility();
    }

    private void ApplyChantVisibility()
    {
        float alpha = Mathf.SmoothStep(0f, 1f, _chantVisibility);
        UI.SkillChain.CanvasGroup.alpha = _skillChainVisibleAlpha * alpha;
        UI.Bar.CanvasGroup.alpha = _chantBarVisibleAlpha * alpha;
        UI.SkillChain.CanvasGroup.interactable = false;
        UI.SkillChain.CanvasGroup.blocksRaycasts = false;
        UI.Bar.CanvasGroup.interactable = false;
        UI.Bar.CanvasGroup.blocksRaycasts = false;
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
            UI.IndexNum.TextMeshProUGUI.text = string.Empty;
            UI.Select.GameObject.SetActive(false);
            return;
        }

        UI.SkillMask_Skill.Image.sprite = LoadIcon(data.SkillIconPath);
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
    public UINode IndexNum;
    public UINode Select;

    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        SkillMask = UINode.From(Find(root, "SkillMask"));
        SkillMask_Skill = UINode.From(Find(root, "SkillMask/Skill"));
        IndexNum = UINode.From(Find(root, "IndexNum"));
        Select = UINode.From(Find(root, "Select"));
    }
}

public class BattleUI_BuffItemView : UISubView<BattleUI_BuffItemData>
{
    public void Render(UnitHealthBarBuffDisplayData data)
    {
        Rebind();
        UI.Icon.Image.sprite = data != null ? LoadIcon(data.IconPath) : null;

        bool showStackCount = data != null && data.StackCount > 1;
        UI.StackCount.GameObject.SetActive(showStackCount);
        UI.StackCount.TextMeshProUGUI.text = showStackCount
            ? data.StackCount.ToString()
            : string.Empty;
    }

    private Sprite LoadIcon(string iconPath)
    {
        return string.IsNullOrWhiteSpace(iconPath) ? null : LoadManagedSprite(iconPath);
    }
}

public class BattleUI_BuffItemData : UIData
{
    public UINode Icon;
    public UINode StackCount;

    public override void Bind(Transform root)
    {
        Icon = UINode.From(root.gameObject);
        StackCount = UINode.From(Find(root, "StackCount"));
    }
}
