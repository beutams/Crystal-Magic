using CrystalMagic.Core;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class InteractionPromptUI : UIBase<InteractionPromptUIData, InteractionPromptUIModel>
    {
        private static readonly Vector2 Padding = new(28f, 16f);

        protected override void RefreshView()
        {
            UI.Prompt.GameObject.SetActive(Model.Visible);
            if (!Model.Visible)
                return;

            var label = UI.Prompt_Label.TextMeshProUGUI;
            if (label.text != Model.Text)
            {
                label.text = Model.Text;
                UI.Prompt.RectTransform.sizeDelta = label.GetPreferredValues(Model.Text) + Padding;
            }

            UI.Prompt.RectTransform.anchoredPosition = Model.Position;
        }
    }
}
