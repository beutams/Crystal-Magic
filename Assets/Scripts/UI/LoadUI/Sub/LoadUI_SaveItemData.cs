// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class LoadUI_SaveItemData : UIData
{
    public UINode Highlight;
    public UINode Highlight_LeftCorner;
    public UINode Highlight_RightCorner;
    public UINode Badge;
    public UINode Badge_Occupied;
    public UINode Badge_Empty;
    public UINode Badge_Index;
    public UINode Badge_Label;
    public UINode Open;
    public UINode Open_MaxFloorLabel;
    public UINode Open_MaxFloor;
    public UINode Open_TotalRunsLabel;
    public UINode Open_TotalRuns;
    public UINode Open_MoneyLabel;
    public UINode Open_Money;
    public UINode Open_Read;
    public UINode Open_Read_Default;
    public UINode Open_Read_Default_Text;
    public UINode Open_Read_Click;
    public UINode Open_Read_Click_Text;
    public UINode Open_Delete;
    public UINode Close;
    public UINode Close_Empty;
    public UINode Close_Hint;
    public UINode Close_Planets;

    public override void Bind(Transform root)
    {
        Highlight = UINode.From(Find(root, "Highlight"));
        Highlight_LeftCorner = UINode.From(Find(root, "Highlight/LeftCorner"));
        Highlight_RightCorner = UINode.From(Find(root, "Highlight/RightCorner"));
        Badge = UINode.From(Find(root, "Badge"));
        Badge_Occupied = UINode.From(Find(root, "Badge/Occupied"));
        Badge_Empty = UINode.From(Find(root, "Badge/Empty"));
        Badge_Index = UINode.From(Find(root, "Badge/Index"));
        Badge_Label = UINode.From(Find(root, "Badge/Label"));
        Open = UINode.From(Find(root, "Open"));
        Open_MaxFloorLabel = UINode.From(Find(root, "Open/MaxFloorLabel"));
        Open_MaxFloor = UINode.From(Find(root, "Open/MaxFloor"));
        Open_TotalRunsLabel = UINode.From(Find(root, "Open/TotalRunsLabel"));
        Open_TotalRuns = UINode.From(Find(root, "Open/TotalRuns"));
        Open_MoneyLabel = UINode.From(Find(root, "Open/MoneyLabel"));
        Open_Money = UINode.From(Find(root, "Open/Money"));
        Open_Read = UINode.From(Find(root, "Open/Read"));
        Open_Read_Default = UINode.From(Find(root, "Open/Read/Default"));
        Open_Read_Default_Text = UINode.From(Find(root, "Open/Read/Default/Text"));
        Open_Read_Click = UINode.From(Find(root, "Open/Read/Click"));
        Open_Read_Click_Text = UINode.From(Find(root, "Open/Read/Click/Text"));
        Open_Delete = UINode.From(Find(root, "Open/Delete"));
        Close = UINode.From(Find(root, "Close"));
        Close_Empty = UINode.From(Find(root, "Close/Empty"));
        Close_Hint = UINode.From(Find(root, "Close/Hint"));
        Close_Planets = UINode.From(Find(root, "Close/Planets"));
    }
}
