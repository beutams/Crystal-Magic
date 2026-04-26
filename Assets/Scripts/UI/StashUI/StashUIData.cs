// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class StashUIData : UIData
{
    public UINode InventoryView;
    public UINode InventoryView_Viewport;
    public UINode InventoryView_Viewport_Content;
    public UINode InventoryView_Viewport_Content_InventoryItem;
    public UINode InventoryView_Viewport_Content_InventoryItem_Icon;
    public UINode InventoryView_Viewport_Content_InventoryItem_Count;
    public UINode InventoryView_Viewport_Content_InventoryItem_Name;
    public UINode StashView;
    public UINode StashView_Viewport;
    public UINode StashView_Viewport_Content;
    public UINode StashView_Viewport_Content_StashItem;
    public UINode StashView_Viewport_Content_StashItem_Icon;
    public UINode StashView_Viewport_Content_StashItem_Count;
    public UINode StashView_Viewport_Content_StashItem_Name;
    public UINode ButtonList;
    public UINode ButtonList_All;
    public UINode ButtonList_All_Default;
    public UINode ButtonList_All_Select;
    public UINode ButtonList_All_Name;
    public UINode ButtonList_Skill;
    public UINode ButtonList_Skill_Default;
    public UINode ButtonList_Skill_Select;
    public UINode ButtonList_Skill_Name;
    public UINode ButtonList_Equip;
    public UINode ButtonList_Equip_Default;
    public UINode ButtonList_Equip_Select;
    public UINode ButtonList_Equip_Name;
    public UINode ButtonList_Props;
    public UINode ButtonList_Props_Default;
    public UINode ButtonList_Props_Select;
    public UINode ButtonList_Props_Name;
    public UINode Coin;
    public UINode Coin_Money;
    public UINode Coin_MoneyText;

    public override void Bind(Transform root)
    {
        InventoryView = UINode.From(Find(root, "InventoryView"));
        InventoryView_Viewport = UINode.From(Find(root, "InventoryView/Viewport"));
        InventoryView_Viewport_Content = UINode.From(Find(root, "InventoryView/Viewport/Content"));
        InventoryView_Viewport_Content_InventoryItem = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem"));
        InventoryView_Viewport_Content_InventoryItem_Icon = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Icon"));
        InventoryView_Viewport_Content_InventoryItem_Count = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Count"));
        InventoryView_Viewport_Content_InventoryItem_Name = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Name"));
        StashView = UINode.From(Find(root, "StashView"));
        StashView_Viewport = UINode.From(Find(root, "StashView/Viewport"));
        StashView_Viewport_Content = UINode.From(Find(root, "StashView/Viewport/Content"));
        StashView_Viewport_Content_StashItem = UINode.From(Find(root, "StashView/Viewport/Content/StashItem"));
        StashView_Viewport_Content_StashItem_Icon = UINode.From(Find(root, "StashView/Viewport/Content/StashItem/Icon"));
        StashView_Viewport_Content_StashItem_Count = UINode.From(Find(root, "StashView/Viewport/Content/StashItem/Count"));
        StashView_Viewport_Content_StashItem_Name = UINode.From(Find(root, "StashView/Viewport/Content/StashItem/Name"));
        ButtonList = UINode.From(Find(root, "ButtonList"));
        ButtonList_All = UINode.From(Find(root, "ButtonList/All"));
        ButtonList_All_Default = UINode.From(Find(root, "ButtonList/All/Default"));
        ButtonList_All_Select = UINode.From(Find(root, "ButtonList/All/Select"));
        ButtonList_All_Name = UINode.From(Find(root, "ButtonList/All/Name"));
        ButtonList_Skill = UINode.From(Find(root, "ButtonList/Skill"));
        ButtonList_Skill_Default = UINode.From(Find(root, "ButtonList/Skill/Default"));
        ButtonList_Skill_Select = UINode.From(Find(root, "ButtonList/Skill/Select"));
        ButtonList_Skill_Name = UINode.From(Find(root, "ButtonList/Skill/Name"));
        ButtonList_Equip = UINode.From(Find(root, "ButtonList/Equip"));
        ButtonList_Equip_Default = UINode.From(Find(root, "ButtonList/Equip/Default"));
        ButtonList_Equip_Select = UINode.From(Find(root, "ButtonList/Equip/Select"));
        ButtonList_Equip_Name = UINode.From(Find(root, "ButtonList/Equip/Name"));
        ButtonList_Props = UINode.From(Find(root, "ButtonList/Props"));
        ButtonList_Props_Default = UINode.From(Find(root, "ButtonList/Props/Default"));
        ButtonList_Props_Select = UINode.From(Find(root, "ButtonList/Props/Select"));
        ButtonList_Props_Name = UINode.From(Find(root, "ButtonList/Props/Name"));
        Coin = UINode.From(Find(root, "Coin"));
        Coin_Money = UINode.From(Find(root, "Coin/Money"));
        Coin_MoneyText = UINode.From(Find(root, "Coin/MoneyText"));
    }
}
