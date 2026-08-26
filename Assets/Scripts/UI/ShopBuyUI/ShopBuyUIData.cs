// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class ShopBuyUIData : UIData
{
    public UINode Background;
    public UINode IconBack;
    public UINode IconBack_Mask;
    public UINode IconBack_Mask_Icon;
    public UINode Name;
    public UINode Have;
    public UINode HaveCount;
    public UINode Description;
    public UINode Add;
    public UINode Reduce;
    public UINode Input;
    public UINode Input_TextArea;
    public UINode Input_TextArea_Text;
    public UINode Coin;
    public UINode Coin_Money;
    public UINode Coin_MoneyText;
    public UINode Sure;
    public UINode Sure_Text;
    public UINode Cancel;
    public UINode Cancel_Text;

    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        IconBack = UINode.From(Find(root, "IconBack"));
        IconBack_Mask = UINode.From(Find(root, "IconBack/Mask"));
        IconBack_Mask_Icon = UINode.From(Find(root, "IconBack/Mask/Icon"));
        Name = UINode.From(Find(root, "Name"));
        Have = UINode.From(Find(root, "Have"));
        HaveCount = UINode.From(Find(root, "HaveCount"));
        Description = UINode.From(Find(root, "Description"));
        Add = UINode.From(Find(root, "Add"));
        Reduce = UINode.From(Find(root, "Reduce"));
        Input = UINode.From(Find(root, "Input"));
        Input_TextArea = UINode.From(Find(root, "Input/Text Area"));
        Input_TextArea_Text = UINode.From(Find(root, "Input/Text Area/Text"));
        Coin = UINode.From(Find(root, "Coin"));
        Coin_Money = UINode.From(Find(root, "Coin/Money"));
        Coin_MoneyText = UINode.From(Find(root, "Coin/MoneyText"));
        Sure = UINode.From(Find(root, "Sure"));
        Sure_Text = UINode.From(Find(root, "Sure/Text"));
        Cancel = UINode.From(Find(root, "Cancel"));
        Cancel_Text = UINode.From(Find(root, "Cancel/Text"));
    }
}
