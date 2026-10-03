// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class BattleUIData : UIData
{
    public UINode SkillChain;
    public UINode SkillChain_BG;
    public UINode SkillChain_SkillItem;
    public UINode SkillChain_SkillItem_Background;
    public UINode SkillChain_SkillItem_SkillMask;
    public UINode SkillChain_SkillItem_SkillMask_Skill;
    public UINode SkillChain_SkillItem_IndexNum;
    public UINode SkillChain_SkillItem_Select;
    public UINode SkillChain_SkillItem1;
    public UINode SkillChain_SkillItem1_Background;
    public UINode SkillChain_SkillItem1_SkillMask;
    public UINode SkillChain_SkillItem1_SkillMask_Skill;
    public UINode SkillChain_SkillItem1_IndexNum;
    public UINode SkillChain_SkillItem1_Select;
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
    public UINode HP_BuffRoot;
    public UINode HP_BuffRoot_BuffIcon;
    public UINode HP_BuffRoot_BuffIcon_StackCount;
    public UINode MP;
    public UINode MP_BarIcon;
    public UINode MP_BarMask;
    public UINode MP_BarMask_Bar;
    public UINode MP_Border;
    public UINode MP_Value;
    public UINode PropShortcuts;
    public UINode PropShortcuts_PropSlot1;
    public UINode PropShortcuts_PropSlot1_Icon;
    public UINode PropShortcuts_PropSlot1_Cooldown;
    public UINode PropShortcuts_PropSlot1_Count;
    public UINode PropShortcuts_PropSlot1_Key;
    public UINode PropShortcuts_PropSlot2;
    public UINode PropShortcuts_PropSlot2_Icon;
    public UINode PropShortcuts_PropSlot2_Cooldown;
    public UINode PropShortcuts_PropSlot2_Count;
    public UINode PropShortcuts_PropSlot2_Key;
    public UINode PropShortcuts_PropSlot3;
    public UINode PropShortcuts_PropSlot3_Icon;
    public UINode PropShortcuts_PropSlot3_Cooldown;
    public UINode PropShortcuts_PropSlot3_Count;
    public UINode PropShortcuts_PropSlot3_Key;

    public override void Bind(Transform root)
    {
        SkillChain = UINode.From(Find(root, "SkillChain"));
        SkillChain_BG = UINode.From(Find(root, "SkillChain/BG"));
        SkillChain_SkillItem = UINode.From(Find(root, "SkillChain/SkillItem"));
        SkillChain_SkillItem_Background = UINode.From(Find(root, "SkillChain/SkillItem/Background"));
        SkillChain_SkillItem_SkillMask = UINode.From(Find(root, "SkillChain/SkillItem/SkillMask"));
        SkillChain_SkillItem_SkillMask_Skill = UINode.From(Find(root, "SkillChain/SkillItem/SkillMask/Skill"));
        SkillChain_SkillItem_IndexNum = UINode.From(Find(root, "SkillChain/SkillItem/IndexNum"));
        SkillChain_SkillItem_Select = UINode.From(Find(root, "SkillChain/SkillItem/Select"));
        SkillChain_SkillItem1 = UINode.From(Find(root, "SkillChain/SkillItem (1)"));
        SkillChain_SkillItem1_Background = UINode.From(Find(root, "SkillChain/SkillItem (1)/Background"));
        SkillChain_SkillItem1_SkillMask = UINode.From(Find(root, "SkillChain/SkillItem (1)/SkillMask"));
        SkillChain_SkillItem1_SkillMask_Skill = UINode.From(Find(root, "SkillChain/SkillItem (1)/SkillMask/Skill"));
        SkillChain_SkillItem1_IndexNum = UINode.From(Find(root, "SkillChain/SkillItem (1)/IndexNum"));
        SkillChain_SkillItem1_Select = UINode.From(Find(root, "SkillChain/SkillItem (1)/Select"));
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
        HP_BuffRoot = UINode.From(Find(root, "HP/BuffRoot"));
        HP_BuffRoot_BuffIcon = UINode.From(Find(root, "HP/BuffRoot/BuffIcon"));
        HP_BuffRoot_BuffIcon_StackCount = UINode.From(Find(root, "HP/BuffRoot/BuffIcon/StackCount"));
        MP = UINode.From(Find(root, "MP"));
        MP_BarIcon = UINode.From(Find(root, "MP/BarIcon"));
        MP_BarMask = UINode.From(Find(root, "MP/BarMask"));
        MP_BarMask_Bar = UINode.From(Find(root, "MP/BarMask/Bar"));
        MP_Border = UINode.From(Find(root, "MP/Border"));
        MP_Value = UINode.From(Find(root, "MP/Value"));
        PropShortcuts = UINode.From(Find(root, "PropShortcuts"));
        PropShortcuts_PropSlot1 = UINode.From(Find(root, "PropShortcuts/PropSlot1"));
        PropShortcuts_PropSlot1_Icon = UINode.From(Find(root, "PropShortcuts/PropSlot1/Icon"));
        PropShortcuts_PropSlot1_Cooldown = UINode.From(Find(root, "PropShortcuts/PropSlot1/Cooldown"));
        PropShortcuts_PropSlot1_Count = UINode.From(Find(root, "PropShortcuts/PropSlot1/Count"));
        PropShortcuts_PropSlot1_Key = UINode.From(Find(root, "PropShortcuts/PropSlot1/Key"));
        PropShortcuts_PropSlot2 = UINode.From(Find(root, "PropShortcuts/PropSlot2"));
        PropShortcuts_PropSlot2_Icon = UINode.From(Find(root, "PropShortcuts/PropSlot2/Icon"));
        PropShortcuts_PropSlot2_Cooldown = UINode.From(Find(root, "PropShortcuts/PropSlot2/Cooldown"));
        PropShortcuts_PropSlot2_Count = UINode.From(Find(root, "PropShortcuts/PropSlot2/Count"));
        PropShortcuts_PropSlot2_Key = UINode.From(Find(root, "PropShortcuts/PropSlot2/Key"));
        PropShortcuts_PropSlot3 = UINode.From(Find(root, "PropShortcuts/PropSlot3"));
        PropShortcuts_PropSlot3_Icon = UINode.From(Find(root, "PropShortcuts/PropSlot3/Icon"));
        PropShortcuts_PropSlot3_Cooldown = UINode.From(Find(root, "PropShortcuts/PropSlot3/Cooldown"));
        PropShortcuts_PropSlot3_Count = UINode.From(Find(root, "PropShortcuts/PropSlot3/Count"));
        PropShortcuts_PropSlot3_Key = UINode.From(Find(root, "PropShortcuts/PropSlot3/Key"));
    }
}
