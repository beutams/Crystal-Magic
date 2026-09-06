using CrystalMagic.Core;
using UnityEngine;

public sealed class MinimapUIData : UIData
{
    public UINode Panel;
    public UINode Terrain;
    public UINode InterestPointRoot;
    public MinimapInterestPointView InterestPointTemplate;
    public UINode Exit;
    public UINode Player;

    public override void Bind(Transform root)
    {
        Panel = UINode.From(Find(root, "Panel"));
        Terrain = UINode.From(Find(root, "Panel/Terrain"));
        InterestPointRoot = UINode.From(Find(root, "Panel/Terrain/InterestPointRoot"));
        InterestPointTemplate = Find(root, "Panel/Terrain/InterestPointRoot/InterestPointTemplate").GetComponent<MinimapInterestPointView>();
        Exit = UINode.From(Find(root, "Panel/Terrain/Exit"));
        Player = UINode.From(Find(root, "Panel/Terrain/Player"));
    }
}
