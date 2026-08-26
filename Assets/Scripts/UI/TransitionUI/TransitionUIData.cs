// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class TransitionUIData : UIData
{
    public UINode Black;
    public UINode Loading;
    public UINode BarMask;
    public UINode BarMask_Bar;
    public UINode BarBroder;
    public UINode Debug;
    public UINode Debug_LoadingTitle;
    public UINode Debug_LoadingDetail;

    public override void Bind(Transform root)
    {
        Black = UINode.From(Find(root, "Black"));
        Loading = UINode.From(Find(root, "Loading"));
        BarMask = UINode.From(Find(root, "BarMask"));
        BarMask_Bar = UINode.From(Find(root, "BarMask/Bar"));
        BarBroder = UINode.From(Find(root, "BarBroder"));
        Debug = UINode.From(Find(root, "Debug"));
        Debug_LoadingTitle = UINode.From(Find(root, "Debug/LoadingTitle"));
        Debug_LoadingDetail = UINode.From(Find(root, "Debug/LoadingDetail"));
    }
}
