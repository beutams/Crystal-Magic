// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class CharacterUI_SkillItemData : UIData
{
    public UINode Background;
    public UINode SkillMask;
    public UINode SkillMask_Skill;
    public UINode Effect;
    public UINode Effect_EffectIcon;
    public UINode IndexNum;
    public UINode NameLabel;
    public UINode StatsLabel;
    public UINode Connector;
    public UINode Select;
    public UINode Select__1;
    public UINode Select__2;
    public UINode Select__3;
    public UINode Select__4;

    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        SkillMask = UINode.From(Find(root, "SkillMask"));
        SkillMask_Skill = UINode.From(Find(root, "SkillMask/Skill"));
        Effect = UINode.From(Find(root, "Effect"));
        Effect_EffectIcon = UINode.From(Find(root, "Effect/EffectIcon"));
        IndexNum = UINode.From(Find(root, "IndexNum"));
        NameLabel = UINode.From(Find(root, "NameLabel"));
        StatsLabel = UINode.From(Find(root, "StatsLabel"));
        Connector = UINode.From(Find(root, "Connector"));
        Select = UINode.From(Find(root, "Select"));
        Select__1 = UINode.From(Find(root, "Select/1"));
        Select__2 = UINode.From(Find(root, "Select/2"));
        Select__3 = UINode.From(Find(root, "Select/3"));
        Select__4 = UINode.From(Find(root, "Select/4"));
    }
}
