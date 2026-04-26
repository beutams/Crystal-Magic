// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class CharacterUIData : UIData
{
    public UINode Skill;
    public UINode Skill_ChangeSkillBtn;
    public UINode Skill_SkillChain;
    public UINode Skill_SkillChain_Viewport;
    public UINode Skill_SkillChain_Viewport_Content;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_Background;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_Skill;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_Effect;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_Index;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_Index_IndexNum;
    public UINode Equip;
    public UINode Equip_Weapen;
    public UINode Equip_Equip1;
    public UINode Equip_Equip2;
    public UINode Equip_Equip3;
    public UINode Equip_Equip4;
    public UINode InventoryView;
    public UINode InventoryView_Viewport;
    public UINode InventoryView_Viewport_Content;
    public UINode InventoryView_Viewport_Content_InventoryItem;
    public UINode InventoryView_Viewport_Content_InventoryItem_Icon;
    public UINode InventoryView_Viewport_Content_InventoryItem_Count;
    public UINode InventoryView_Viewport_Content_InventoryItem_Name;

    public override void Bind(Transform root)
    {
        Skill = UINode.From(Find(root, "Skill"));
        Skill_ChangeSkillBtn = UINode.From(Find(root, "Skill/ChangeSkillBtn"));
        Skill_SkillChain = UINode.From(Find(root, "Skill/SkillChain"));
        Skill_SkillChain_Viewport = UINode.From(Find(root, "Skill/SkillChain/Viewport"));
        Skill_SkillChain_Viewport_Content = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content"));
        Skill_SkillChain_Viewport_Content_SkillItem = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem"));
        Skill_SkillChain_Viewport_Content_SkillItem_Background = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/Background"));
        Skill_SkillChain_Viewport_Content_SkillItem_Skill = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/Skill"));
        Skill_SkillChain_Viewport_Content_SkillItem_Effect = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/Effect"));
        Skill_SkillChain_Viewport_Content_SkillItem_Index = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/Index"));
        Skill_SkillChain_Viewport_Content_SkillItem_Index_IndexNum = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/Index/IndexNum"));
        Equip = UINode.From(Find(root, "Equip"));
        Equip_Weapen = UINode.From(Find(root, "Equip/Weapen"));
        Equip_Equip1 = UINode.From(Find(root, "Equip/Equip1"));
        Equip_Equip2 = UINode.From(Find(root, "Equip/Equip2"));
        Equip_Equip3 = UINode.From(Find(root, "Equip/Equip3"));
        Equip_Equip4 = UINode.From(Find(root, "Equip/Equip4"));
        InventoryView = UINode.From(Find(root, "InventoryView"));
        InventoryView_Viewport = UINode.From(Find(root, "InventoryView/Viewport"));
        InventoryView_Viewport_Content = UINode.From(Find(root, "InventoryView/Viewport/Content"));
        InventoryView_Viewport_Content_InventoryItem = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem"));
        InventoryView_Viewport_Content_InventoryItem_Icon = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Icon"));
        InventoryView_Viewport_Content_InventoryItem_Count = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Count"));
        InventoryView_Viewport_Content_InventoryItem_Name = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Name"));
    }
}
