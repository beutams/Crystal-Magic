// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class CharacterUI_SkillItemData : UIData
{
    public UINode Background;
    public UINode Skill;
    public UINode Effect;

    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        Skill = UINode.From(Find(root, "Skill"));
        Effect = UINode.From(Find(root, "Effect"));
    }
}
