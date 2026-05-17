// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class GameSaveUI_SaveItemData : UIData
{
    public UINode Open;
    public UINode Open_CreateTime;
    public UINode Open_Money;
    public UINode Open_Index;
    public UINode Open_Delete;
    public UINode Close;

    public override void Bind(Transform root)
    {
        Open = UINode.From(Find(root, "Open"));
        Open_CreateTime = UINode.From(Find(root, "Open/CreateTime"));
        Open_Money = UINode.From(Find(root, "Open/Money"));
        Open_Index = UINode.From(Find(root, "Open/Index"));
        Open_Delete = UINode.From(Find(root, "Open/Delete"));
        Close = UINode.From(Find(root, "Close"));
    }
}
