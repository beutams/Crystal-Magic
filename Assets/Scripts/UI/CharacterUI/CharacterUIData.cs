// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class CharacterUIData : UIData
{
    public UINode Skill;
    public UINode Skill_SkillChain;
    public UINode Skill_SkillChain_Viewport;
    public UINode Skill_SkillChain_Viewport_Content;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_Background;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_SkillMask;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_SkillMask_Skill;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_Effect;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_Effect_EffectIcon;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_IndexNum;
    public UINode Equip;
    public UINode Equip_MagicStoneBorder;
    public UINode Equip_MagicStoneBorder_MagicStone;
    public UINode Equip_Equip1Border;
    public UINode Equip_Equip1Border_Equip1;
    public UINode Equip_Equip2Border;
    public UINode Equip_Equip2Border_Equip2;
    public UINode Equip_Equip3Border;
    public UINode Equip_Equip3Border_Equip3;
    public UINode Equip_Equip4Border;
    public UINode Equip_Equip4Border_Equip4;
    public UINode InventoryView;
    public UINode InventoryView_Viewport;
    public UINode InventoryView_Viewport_Content;
    public UINode InventoryView_Viewport_Content_InventoryItem;
    public UINode InventoryView_Viewport_Content_InventoryItem_Mask;
    public UINode InventoryView_Viewport_Content_InventoryItem_Mask_Icon;
    public UINode InventoryView_Viewport_Content_InventoryItem_Count;
    public UINode InventoryView_Viewport_Content_InventoryItem_Name;
    public UINode ItemDrag;
    public UINode ItemDrag_Mask;
    public UINode ItemDrag_Mask_Icon;
    public UINode SkillDrag;
    public UINode SkillDrag_Mask;
    public UINode SkillDrag_Mask_Icon;

    public override void Bind(Transform root)
    {
        Skill = UINode.From(Find(root, "Skill"));
        Skill_SkillChain = UINode.From(Find(root, "Skill/SkillChain"));
        Skill_SkillChain_Viewport = UINode.From(Find(root, "Skill/SkillChain/Viewport"));
        Skill_SkillChain_Viewport_Content = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content"));
        Skill_SkillChain_Viewport_Content_SkillItem = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem"));
        Skill_SkillChain_Viewport_Content_SkillItem_Background = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/Background"));
        Skill_SkillChain_Viewport_Content_SkillItem_SkillMask = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/SkillMask"));
        Skill_SkillChain_Viewport_Content_SkillItem_SkillMask_Skill = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/SkillMask/Skill"));
        Skill_SkillChain_Viewport_Content_SkillItem_Effect = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/Effect"));
        Skill_SkillChain_Viewport_Content_SkillItem_Effect_EffectIcon = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/Effect/EffectIcon"));
        Skill_SkillChain_Viewport_Content_SkillItem_IndexNum = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/IndexNum"));
        Equip = UINode.From(Find(root, "Equip"));
        Equip_MagicStoneBorder = UINode.From(Find(root, "Equip/MagicStoneBorder"));
        Equip_MagicStoneBorder_MagicStone = UINode.From(Find(root, "Equip/MagicStoneBorder/MagicStone"));
        Equip_Equip1Border = UINode.From(Find(root, "Equip/Equip1Border"));
        Equip_Equip1Border_Equip1 = UINode.From(Find(root, "Equip/Equip1Border/Equip1"));
        Equip_Equip2Border = UINode.From(Find(root, "Equip/Equip2Border"));
        Equip_Equip2Border_Equip2 = UINode.From(Find(root, "Equip/Equip2Border/Equip2"));
        Equip_Equip3Border = UINode.From(Find(root, "Equip/Equip3Border"));
        Equip_Equip3Border_Equip3 = UINode.From(Find(root, "Equip/Equip3Border/Equip3"));
        Equip_Equip4Border = UINode.From(Find(root, "Equip/Equip4Border"));
        Equip_Equip4Border_Equip4 = UINode.From(Find(root, "Equip/Equip4Border/Equip4"));
        InventoryView = UINode.From(Find(root, "InventoryView"));
        InventoryView_Viewport = UINode.From(Find(root, "InventoryView/Viewport"));
        InventoryView_Viewport_Content = UINode.From(Find(root, "InventoryView/Viewport/Content"));
        InventoryView_Viewport_Content_InventoryItem = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem"));
        InventoryView_Viewport_Content_InventoryItem_Mask = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Mask"));
        InventoryView_Viewport_Content_InventoryItem_Mask_Icon = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Mask/Icon"));
        InventoryView_Viewport_Content_InventoryItem_Count = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Count"));
        InventoryView_Viewport_Content_InventoryItem_Name = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Name"));
        ItemDrag = UINode.From(Find(root, "ItemDrag"));
        ItemDrag_Mask = UINode.From(Find(root, "ItemDrag/Mask"));
        ItemDrag_Mask_Icon = UINode.From(Find(root, "ItemDrag/Mask/Icon"));
        SkillDrag = UINode.From(Find(root, "SkillDrag"));
        SkillDrag_Mask = UINode.From(Find(root, "SkillDrag/Mask"));
        SkillDrag_Mask_Icon = UINode.From(Find(root, "SkillDrag/Mask/Icon"));
    }
}
