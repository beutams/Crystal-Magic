using CrystalMagic.Core;

public class CharacterUI_SkillItemView : UISubView<CharacterUI_SkillItemData>
{
    public void Render(CrystalMagic.UI.CharacterSkillDisplayData data)
    {
        Rebind();

        if (data == null)
        {
            UI.Skill.Image.sprite = null;
            UI.Effect_EffectIcon.Image.sprite = null;
            UI.Index_IndexNum.TextMeshProUGUI.text = string.Empty;
            return;
        }

        UI.Index_IndexNum.TextMeshProUGUI.text = data.DisplayIndex.ToString();
        UI.Skill.Image.sprite = LoadIcon(data.SkillIconPath);
        UI.Effect_EffectIcon.Image.sprite = LoadIcon(data.EffectIconPath);
    }

    private UnityEngine.Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<UnityEngine.Sprite>(iconPath);
    }
}
