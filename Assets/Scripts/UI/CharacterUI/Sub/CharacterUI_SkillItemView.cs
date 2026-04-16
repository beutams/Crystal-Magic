using CrystalMagic.Core;

public class CharacterUI_SkillItemView : UISubView<CharacterUI_SkillItemData>
{
    public void Render(CrystalMagic.UI.CharacterSkillDisplayData data)
    {
        Rebind();

        if (data == null)
        {
            UI.Skill.Image.sprite = null;
            UI.Effect.Image.sprite = null;
            return;
        }

        UI.Skill.Image.sprite = LoadIcon(data.SkillIconPath);
        UI.Effect.Image.sprite = LoadIcon(data.EffectIconPath);
    }

    private UnityEngine.Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<UnityEngine.Sprite>(iconPath);
    }
}
