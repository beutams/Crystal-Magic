// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class BattleUIData : UIData
{
    public UINode SkillChain;
    public UINode SkillChain_Viewport;
    public UINode SkillChain_Viewport_Content;
    public UINode SkillChain_Viewport_Content_SkillItem;
    public UINode SkillChain_Viewport_Content_SkillItem_Background;
    public UINode SkillChain_Viewport_Content_SkillItem_SkillMask;
    public UINode SkillChain_Viewport_Content_SkillItem_SkillMask_Skill;
    public UINode SkillChain_Viewport_Content_SkillItem_Effect;
    public UINode SkillChain_Viewport_Content_SkillItem_Effect_EffectIcon;
    public UINode SkillChain_Viewport_Content_SkillItem_IndexNum;
    public UINode SkillChain_Viewport_Content_SkillItem_Select;
    public UINode Bar;
    public UINode Bar_BarMask;
    public UINode Bar_BarMask_Bar;
    public UINode Bar_Border;
    public UINode HP;
    public UINode HP_BarIcon;
    public UINode HP_BarMask;
    public UINode HP_BarMask_Bar;
    public UINode HP_Border;
    public UINode HP_Value;
    public UINode MP;
    public UINode MP_BarIcon;
    public UINode MP_BarMask;
    public UINode MP_BarMask_Bar;
    public UINode MP_Border;
    public UINode MP_Value;

    public override void Bind(Transform root)
    {
        SkillChain = UINode.From(Find(root, "SkillChain"));
        SkillChain_Viewport = UINode.From(Find(root, "SkillChain/Viewport"));
        SkillChain_Viewport_Content = UINode.From(Find(root, "SkillChain/Viewport/Content"));
        SkillChain_Viewport_Content_SkillItem = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem"));
        SkillChain_Viewport_Content_SkillItem_Background = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Background"));
        SkillChain_Viewport_Content_SkillItem_SkillMask = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/SkillMask"));
        SkillChain_Viewport_Content_SkillItem_SkillMask_Skill = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/SkillMask/Skill"));
        SkillChain_Viewport_Content_SkillItem_Effect = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Effect"));
        SkillChain_Viewport_Content_SkillItem_Effect_EffectIcon = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Effect/EffectIcon"));
        SkillChain_Viewport_Content_SkillItem_IndexNum = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/IndexNum"));
        SkillChain_Viewport_Content_SkillItem_Select = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Select"));
        Bar = UINode.From(Find(root, "Bar"));
        Bar_BarMask = UINode.From(Find(root, "Bar/BarMask"));
        Bar_BarMask_Bar = UINode.From(Find(root, "Bar/BarMask/Bar"));
        Bar_Border = UINode.From(Find(root, "Bar/Border"));
        HP = UINode.From(Find(root, "HP"));
        HP_BarIcon = UINode.From(Find(root, "HP/BarIcon"));
        HP_BarMask = UINode.From(Find(root, "HP/BarMask"));
        HP_BarMask_Bar = UINode.From(Find(root, "HP/BarMask/Bar"));
        HP_Border = UINode.From(Find(root, "HP/Border"));
        HP_Value = UINode.From(Find(root, "HP/Value"));
        MP = UINode.From(Find(root, "MP"));
        MP_BarIcon = UINode.From(Find(root, "MP/BarIcon"));
        MP_BarMask = UINode.From(Find(root, "MP/BarMask"));
        MP_BarMask_Bar = UINode.From(Find(root, "MP/BarMask/Bar"));
        MP_Border = UINode.From(Find(root, "MP/Border"));
        MP_Value = UINode.From(Find(root, "MP/Value"));
    }
}
