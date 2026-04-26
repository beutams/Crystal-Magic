// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class ShopSellUIData : UIData
{
    public UINode Background;
    public UINode IconBack;
    public UINode IconBack_Icon;
    public UINode Name;
    public UINode Have;
    public UINode HaveCount;
    public UINode Description;
    public UINode Add;
    public UINode Reduce;
    public UINode Input;
    public UINode Input_TextArea;
    public UINode Input_TextArea_Text;
    public UINode Sure;
    public UINode Cancel;

    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        IconBack = UINode.From(Find(root, "IconBack"));
        IconBack_Icon = UINode.From(Find(root, "IconBack/Icon"));
        Name = UINode.From(Find(root, "Name"));
        Have = UINode.From(Find(root, "Have"));
        HaveCount = UINode.From(Find(root, "HaveCount"));
        Description = UINode.From(Find(root, "Description"));
        Add = UINode.From(Find(root, "Add"));
        Reduce = UINode.From(Find(root, "Reduce"));
        Input = UINode.From(Find(root, "Input"));
        Input_TextArea = UINode.From(Find(root, "Input/Text Area"));
        Input_TextArea_Text = UINode.From(Find(root, "Input/Text Area/Text"));
        Sure = UINode.From(Find(root, "Sure"));
        Cancel = UINode.From(Find(root, "Cancel"));
    }
}
