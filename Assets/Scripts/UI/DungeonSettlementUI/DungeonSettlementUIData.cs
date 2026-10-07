// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class DungeonSettlementUIData : UIData
{
    public UINode Backdrop;
    public UINode Panel;
    public UINode Panel_BG;
    public UINode Panel_BG_ChainLeft;
    public UINode Panel_BG_ChainRight;
    public UINode Panel_BG_Header;
    public UINode Panel_BG_Crest;
    public UINode Panel_BG_Crest_Star;
    public UINode Panel_BG_CaptionLeft;
    public UINode Panel_BG_CaptionRight;
    public UINode Panel_Title;
    public UINode Panel_Summary;
    public UINode Panel_Confirm;
    public UINode Panel_Confirm_Label;
    public UINode Panel_Money;
    public UINode Panel_Caption;
    public UINode Panel_Items;
    public UINode Panel_Items_Viewport;
    public UINode Panel_Items_Viewport_Content;
    public UINode Panel_Items_Viewport_Content_Item;
    public UINode Panel_Items_Viewport_Content_Item_Socket;
    public UINode Panel_Items_Viewport_Content_Item_Socket_Icon;
    public UINode Panel_Items_Viewport_Content_Item_Socket_AmountBG;
    public UINode Panel_Items_Viewport_Content_Item_Socket_Amount;
    public UINode Panel_Items_Viewport_Content_Item_Name;
    public UINode Panel_Items_Viewport_Content_Item_Selected;
    public UINode Panel_Empty;
    public UINode Panel_Detail;
    public UINode Panel_Footnote;

    public override void Bind(Transform root)
    {
        Backdrop = UINode.From(Find(root, "Backdrop"));
        Panel = UINode.From(Find(root, "Panel"));
        Panel_BG = UINode.From(Find(root, "Panel/BG"));
        Panel_BG_ChainLeft = UINode.From(Find(root, "Panel/BG/ChainLeft"));
        Panel_BG_ChainRight = UINode.From(Find(root, "Panel/BG/ChainRight"));
        Panel_BG_Header = UINode.From(Find(root, "Panel/BG/Header"));
        Panel_BG_Crest = UINode.From(Find(root, "Panel/BG/Crest"));
        Panel_BG_Crest_Star = UINode.From(Find(root, "Panel/BG/Crest/Star"));
        Panel_BG_CaptionLeft = UINode.From(Find(root, "Panel/BG/CaptionLeft"));
        Panel_BG_CaptionRight = UINode.From(Find(root, "Panel/BG/CaptionRight"));
        Panel_Title = UINode.From(Find(root, "Panel/Title"));
        Panel_Summary = UINode.From(Find(root, "Panel/Summary"));
        Panel_Confirm = UINode.From(Find(root, "Panel/Confirm"));
        Panel_Confirm_Label = UINode.From(Find(root, "Panel/Confirm/Label"));
        Panel_Money = UINode.From(Find(root, "Panel/Money"));
        Panel_Caption = UINode.From(Find(root, "Panel/Caption"));
        Panel_Items = UINode.From(Find(root, "Panel/Items"));
        Panel_Items_Viewport = UINode.From(Find(root, "Panel/Items/Viewport"));
        Panel_Items_Viewport_Content = UINode.From(Find(root, "Panel/Items/Viewport/Content"));
        Panel_Items_Viewport_Content_Item = UINode.From(Find(root, "Panel/Items/Viewport/Content/Item"));
        Panel_Items_Viewport_Content_Item_Socket = UINode.From(Find(root, "Panel/Items/Viewport/Content/Item/Socket"));
        Panel_Items_Viewport_Content_Item_Socket_Icon = UINode.From(Find(root, "Panel/Items/Viewport/Content/Item/Socket/Icon"));
        Panel_Items_Viewport_Content_Item_Socket_AmountBG = UINode.From(Find(root, "Panel/Items/Viewport/Content/Item/Socket/AmountBG"));
        Panel_Items_Viewport_Content_Item_Socket_Amount = UINode.From(Find(root, "Panel/Items/Viewport/Content/Item/Socket/Amount"));
        Panel_Items_Viewport_Content_Item_Name = UINode.From(Find(root, "Panel/Items/Viewport/Content/Item/Name"));
        Panel_Items_Viewport_Content_Item_Selected = UINode.From(Find(root, "Panel/Items/Viewport/Content/Item/Selected"));
        Panel_Empty = UINode.From(Find(root, "Panel/Empty"));
        Panel_Detail = UINode.From(Find(root, "Panel/Detail"));
        Panel_Footnote = UINode.From(Find(root, "Panel/Footnote"));
    }
}
