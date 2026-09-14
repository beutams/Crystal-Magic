// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class ShopUIData : UIData
{
    public UINode ShopView;
    public UINode ShopView_Viewport;
    public UINode ShopView_Viewport_Content;
    public UINode ShopView_Viewport_Content_CommodityItem;
    public UINode ShopView_Viewport_Content_CommodityItem_Select;
    public UINode ShopView_Viewport_Content_CommodityItem_IconBG;
    public UINode ShopView_Viewport_Content_CommodityItem_IconBG_Mask;
    public UINode ShopView_Viewport_Content_CommodityItem_IconBG_Mask_Icon;
    public UINode ShopView_Viewport_Content_CommodityItem_Name;
    public UINode ShopView_Viewport_Content_CommodityItem_Description;
    public UINode ShopView_Viewport_Content_CommodityItem_Coin;
    public UINode ShopView_Viewport_Content_CommodityItem_Price;
    public UINode InventoryView;
    public UINode InventoryView_Viewport;
    public UINode InventoryView_Viewport_Content;
    public UINode InventoryView_Viewport_Content_InventoryItem;
    public UINode InventoryView_Viewport_Content_InventoryItem_IconBG;
    public UINode InventoryView_Viewport_Content_InventoryItem_IconBG_Mask;
    public UINode InventoryView_Viewport_Content_InventoryItem_IconBG_Mask_Icon;
    public UINode InventoryView_Viewport_Content_InventoryItem_Count;
    public UINode InventoryView_Viewport_Content_InventoryItem_Name;
    public UINode Coin;
    public UINode Coin_Money;
    public UINode Coin_MoneyText;
    public UINode Drag;
    public UINode Drag_Mask;
    public UINode Drag_Mask_Icon;

    public override void Bind(Transform root)
    {
        ShopView = UINode.From(Find(root, "ShopView"));
        ShopView_Viewport = UINode.From(Find(root, "ShopView/Viewport"));
        ShopView_Viewport_Content = UINode.From(Find(root, "ShopView/Viewport/Content"));
        ShopView_Viewport_Content_CommodityItem = UINode.From(Find(root, "ShopView/Viewport/Content/CommodityItem"));
        ShopView_Viewport_Content_CommodityItem_Select = UINode.From(Find(root, "ShopView/Viewport/Content/CommodityItem/Select"));
        ShopView_Viewport_Content_CommodityItem_IconBG = UINode.From(Find(root, "ShopView/Viewport/Content/CommodityItem/IconBG"));
        ShopView_Viewport_Content_CommodityItem_IconBG_Mask = UINode.From(Find(root, "ShopView/Viewport/Content/CommodityItem/IconBG/Mask"));
        ShopView_Viewport_Content_CommodityItem_IconBG_Mask_Icon = UINode.From(Find(root, "ShopView/Viewport/Content/CommodityItem/IconBG/Mask/Icon"));
        ShopView_Viewport_Content_CommodityItem_Name = UINode.From(Find(root, "ShopView/Viewport/Content/CommodityItem/Name"));
        ShopView_Viewport_Content_CommodityItem_Description = UINode.From(Find(root, "ShopView/Viewport/Content/CommodityItem/Description"));
        ShopView_Viewport_Content_CommodityItem_Coin = UINode.From(Find(root, "ShopView/Viewport/Content/CommodityItem/Coin"));
        ShopView_Viewport_Content_CommodityItem_Price = UINode.From(Find(root, "ShopView/Viewport/Content/CommodityItem/Price"));
        InventoryView = UINode.From(Find(root, "InventoryView"));
        InventoryView_Viewport = UINode.From(Find(root, "InventoryView/Viewport"));
        InventoryView_Viewport_Content = UINode.From(Find(root, "InventoryView/Viewport/Content"));
        InventoryView_Viewport_Content_InventoryItem = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem"));
        InventoryView_Viewport_Content_InventoryItem_IconBG = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/IconBG"));
        InventoryView_Viewport_Content_InventoryItem_IconBG_Mask = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/IconBG/Mask"));
        InventoryView_Viewport_Content_InventoryItem_IconBG_Mask_Icon = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/IconBG/Mask/Icon"));
        InventoryView_Viewport_Content_InventoryItem_Count = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Count"));
        InventoryView_Viewport_Content_InventoryItem_Name = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Name"));
        Coin = UINode.From(Find(root, "Coin"));
        Coin_Money = UINode.From(Find(root, "Coin/Money"));
        Coin_MoneyText = UINode.From(Find(root, "Coin/MoneyText"));
        Drag = UINode.From(Find(root, "Drag"));
        Drag_Mask = UINode.From(Find(root, "Drag/Mask"));
        Drag_Mask_Icon = UINode.From(Find(root, "Drag/Mask/Icon"));
    }
}
