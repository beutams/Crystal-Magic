using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;

public class BattleUI : UIBase<BattleUIData, BattleUIModel>
{
    private readonly List<BattleUI_SkillItemView> _skillItemViews = new();
    private float _hpMaskBaseWidth = -1f;
    private float _mpMaskBaseWidth = -1f;

    public override void OnOpen()
    {
        EnsureSkillItemTemplateView();
        CacheBarWidths();
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
        RenderVitalityAndMana(Model.HpRatio, Model.MpRatio, Model.CurrentHp, Model.MaxHp, Model.CurrentMp, Model.MaxMp);
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
        if (templateView == null)
            return;

        while (_skillItemViews.Count < itemCount)
        {
            BattleUI_SkillItemView itemView = UISubViewBase.AcquireFromPool(templateView, UI.SkillChain_Viewport_Content.GameObject.transform);
            if (itemView == null)
                break;

            _skillItemViews.Add(itemView);
        }
    }

    private void EnsureSkillItemTemplateView()
    {
        if (UI.SkillChain_Viewport_Content_SkillItem.GameObject == null)
            return;

        if (UI.SkillChain_Viewport_Content_SkillItem.GameObject.GetComponent<BattleUI_SkillItemView>() == null)
            UI.SkillChain_Viewport_Content_SkillItem.GameObject.AddComponent<BattleUI_SkillItemView>();
    }

    private void RenderVitalityAndMana(float hpRatio, float mpRatio, float currentHp, float maxHp, float currentMp, float maxMp)
    {
        CacheBarWidths();
        SetMaskWidth(UI.HP_BarMask.RectTransform, hpRatio, _hpMaskBaseWidth);
        SetMaskWidth(UI.MP_BarMask.RectTransform, mpRatio, _mpMaskBaseWidth);
        SetValueText(UI.HP_Value.TextMeshProUGUI, currentHp, maxHp);
        SetValueText(UI.MP_Value.TextMeshProUGUI, currentMp, maxMp);
    }

    private void CacheBarWidths()
    {
        if (_hpMaskBaseWidth <= 0f && UI.HP_BarMask.RectTransform != null)
        {
            _hpMaskBaseWidth = UI.HP_BarMask.RectTransform.rect.width;
            if (_hpMaskBaseWidth <= 0f)
                _hpMaskBaseWidth = UI.HP_BarMask.RectTransform.sizeDelta.x;
        }

        if (_mpMaskBaseWidth <= 0f && UI.MP_BarMask.RectTransform != null)
        {
            _mpMaskBaseWidth = UI.MP_BarMask.RectTransform.rect.width;
            if (_mpMaskBaseWidth <= 0f)
                _mpMaskBaseWidth = UI.MP_BarMask.RectTransform.sizeDelta.x;
        }
    }

    private static void SetMaskWidth(RectTransform rectTransform, float ratio, float baseWidth)
    {
        if (rectTransform == null || baseWidth <= 0f)
            return;

        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, baseWidth * Mathf.Clamp01(ratio));
    }

    private static void SetValueText(TMPro.TextMeshProUGUI text, float current, float max)
    {
        if (text == null)
            return;

        text.text = $"{FormatValue(current)}/{FormatValue(max)}";
    }

    private static string FormatValue(float value)
    {
        return Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.#");
    }
}

public class BattleUI_SkillItemView : UISubView<BattleUI_SkillItemData>
{
    private float _baseBarMaskWidth = -1f;

    public void Render(BattleSkillDisplayData data)
    {
        Rebind();
        EnsureBarWidthCached();

        if (data == null)
        {
            UI.Skill.Image.sprite = null;
            UI.Effect_EffectIcon.Image.sprite = null;
            UI.Index_IndexNum.TextMeshProUGUI.text = string.Empty;
            SetSelected(false, false, 0f);
            return;
        }

        UI.Skill.Image.sprite = LoadIcon(data.SkillIconPath);
        UI.Effect_EffectIcon.Image.sprite = LoadIcon(data.AdditionIconPath);
        UI.Index_IndexNum.TextMeshProUGUI.text = data.DisplayIndex.ToString();
        SetSelected(data.IsSelected, data.ShowChantProgress, data.ChantProgress);
    }

    private void SetSelected(bool selected, bool showChantProgress, float chantProgress)
    {
        if (UI.Select.GameObject != null)
            UI.Select.GameObject.SetActive(selected);

        if (UI.Select_BarBackground.GameObject != null)
            UI.Select_BarBackground.GameObject.SetActive(selected && showChantProgress);

        if (UI.Select_BarMask.GameObject != null)
            UI.Select_BarMask.GameObject.SetActive(selected && showChantProgress);

        SetMaskWidth(UI.Select_BarMask.RectTransform, showChantProgress ? chantProgress : 0f, _baseBarMaskWidth);
    }

    private void EnsureBarWidthCached()
    {
        if (_baseBarMaskWidth > 0f || UI.Select_BarMask.RectTransform == null)
            return;

        _baseBarMaskWidth = UI.Select_BarMask.RectTransform.rect.width;
        if (_baseBarMaskWidth <= 0f)
            _baseBarMaskWidth = UI.Select_BarMask.RectTransform.sizeDelta.x;
    }

    private static void SetMaskWidth(RectTransform rectTransform, float ratio, float baseWidth)
    {
        if (rectTransform == null || baseWidth <= 0f)
            return;

        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, baseWidth * Mathf.Clamp01(ratio));
    }

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return LoadManagedResource<Sprite>(iconPath);
    }
}

public class BattleUI_SkillItemData : UIData
{
    public UINode Background;
    public UINode Select;
    public UINode Select_Border;
    public UINode Select_BarBackground;
    public UINode Select_BarMask;
    public UINode Select_BarMask_Bar;
    public UINode Skill;
    public UINode Effect;
    public UINode Effect_EffectIcon;
    public UINode Index;
    public UINode Index_IndexNum;

    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        Select = UINode.From(Find(root, "Select"));
        Select_Border = UINode.From(Find(root, "Select/Border"));
        Select_BarBackground = UINode.From(Find(root, "Select/BarBackground"));
        Select_BarMask = UINode.From(Find(root, "Select/BarMask"));
        Select_BarMask_Bar = UINode.From(Find(root, "Select/BarMask/Bar"));
        Skill = UINode.From(Find(root, "Skill"));
        Effect = UINode.From(Find(root, "Effect"));
        Effect_EffectIcon = UINode.From(Find(root, "Effect/EffectIcon"));
        Index = UINode.From(Find(root, "Index"));
        Index_IndexNum = UINode.From(Find(root, "Index/IndexNum"));
    }
}
