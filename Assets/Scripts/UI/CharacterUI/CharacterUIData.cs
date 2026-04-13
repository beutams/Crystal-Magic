// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class CharacterUIData : UIData
{
    public UINode Skill;
    public UINode Skill_ChangeSkillBtn;
    public UINode Skill_SkillChain;
    public UINode Skill_SkillChain_Viewport;
    public UINode Skill_SkillChain_Viewport_Content;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_Background;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_Skill;
    public UINode Skill_SkillChain_Viewport_Content_SkillItem_Effect;
    public UINode Equip;
    public UINode Inventory;

    public override void Bind(Transform root)
    {
        Skill = UINode.From(Find(root, "Skill"));
        Skill_ChangeSkillBtn = UINode.From(Find(root, "Skill/ChangeSkillBtn"));
        Skill_SkillChain = UINode.From(Find(root, "Skill/SkillChain"));
        Skill_SkillChain_Viewport = UINode.From(Find(root, "Skill/SkillChain/Viewport"));
        Skill_SkillChain_Viewport_Content = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content"));
        Skill_SkillChain_Viewport_Content_SkillItem = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem"));
        Skill_SkillChain_Viewport_Content_SkillItem_Background = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/Background"));
        Skill_SkillChain_Viewport_Content_SkillItem_Skill = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/Skill"));
        Skill_SkillChain_Viewport_Content_SkillItem_Effect = UINode.From(Find(root, "Skill/SkillChain/Viewport/Content/SkillItem/Effect"));
        Equip = UINode.From(Find(root, "Equip"));
        Inventory = UINode.From(Find(root, "Inventory"));
    }
}
