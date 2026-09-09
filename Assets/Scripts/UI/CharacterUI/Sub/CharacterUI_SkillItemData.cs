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

    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        SkillMask = UINode.From(Find(root, "SkillMask"));
        SkillMask_Skill = UINode.From(Find(root, "SkillMask/Skill"));
        Effect = UINode.From(Find(root, "Effect"));
        Effect_EffectIcon = UINode.From(Find(root, "Effect/EffectIcon"));
        IndexNum = UINode.From(Find(root, "IndexNum"));
    }
}
