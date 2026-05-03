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
    public UINode SkillChain_Viewport_Content_SkillItem_Select;
    public UINode SkillChain_Viewport_Content_SkillItem_Select_Border;
    public UINode SkillChain_Viewport_Content_SkillItem_Select_BarBackground;
    public UINode SkillChain_Viewport_Content_SkillItem_Select_BarMask;
    public UINode SkillChain_Viewport_Content_SkillItem_Select_BarMask_Bar;
    public UINode SkillChain_Viewport_Content_SkillItem_Skill;
    public UINode SkillChain_Viewport_Content_SkillItem_Effect;
    public UINode SkillChain_Viewport_Content_SkillItem_Effect_EffectIcon;
    public UINode SkillChain_Viewport_Content_SkillItem_Index;
    public UINode SkillChain_Viewport_Content_SkillItem_Index_IndexNum;
    public UINode HP;
    public UINode HP_BarIcon;
    public UINode HP_BarBackground;
    public UINode HP_BarMask;
    public UINode HP_BarMask_Bar;
    public UINode MP;
    public UINode MP_BarIcon;
    public UINode MP_BarBackground;
    public UINode MP_BarMask;
    public UINode MP_BarMask_Bar;

    public override void Bind(Transform root)
    {
        SkillChain = UINode.From(Find(root, "SkillChain"));
        SkillChain_Viewport = UINode.From(Find(root, "SkillChain/Viewport"));
        SkillChain_Viewport_Content = UINode.From(Find(root, "SkillChain/Viewport/Content"));
        SkillChain_Viewport_Content_SkillItem = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem"));
        SkillChain_Viewport_Content_SkillItem_Background = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Background"));
        SkillChain_Viewport_Content_SkillItem_Select = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Select"));
        SkillChain_Viewport_Content_SkillItem_Select_Border = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Select/Border"));
        SkillChain_Viewport_Content_SkillItem_Select_BarBackground = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Select/BarBackground"));
        SkillChain_Viewport_Content_SkillItem_Select_BarMask = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Select/BarMask"));
        SkillChain_Viewport_Content_SkillItem_Select_BarMask_Bar = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Select/BarMask/Bar"));
        SkillChain_Viewport_Content_SkillItem_Skill = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Skill"));
        SkillChain_Viewport_Content_SkillItem_Effect = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Effect"));
        SkillChain_Viewport_Content_SkillItem_Effect_EffectIcon = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Effect/EffectIcon"));
        SkillChain_Viewport_Content_SkillItem_Index = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Index"));
        SkillChain_Viewport_Content_SkillItem_Index_IndexNum = UINode.From(Find(root, "SkillChain/Viewport/Content/SkillItem/Index/IndexNum"));
        HP = UINode.From(Find(root, "HP"));
        HP_BarIcon = UINode.From(Find(root, "HP/BarIcon"));
        HP_BarBackground = UINode.From(Find(root, "HP/BarBackground"));
        HP_BarMask = UINode.From(Find(root, "HP/BarMask"));
        HP_BarMask_Bar = UINode.From(Find(root, "HP/BarMask/Bar"));
        MP = UINode.From(Find(root, "MP"));
        MP_BarIcon = UINode.From(Find(root, "MP/BarIcon"));
        MP_BarBackground = UINode.From(Find(root, "MP/BarBackground"));
        MP_BarMask = UINode.From(Find(root, "MP/BarMask"));
        MP_BarMask_Bar = UINode.From(Find(root, "MP/BarMask/Bar"));
    }
}
