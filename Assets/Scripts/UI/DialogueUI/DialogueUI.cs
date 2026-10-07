using CrystalMagic.Core;
using TMPro;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class DialogueUI : UIBase<DialogueUIData, DialogueUIModel>
    {
        private const float MaxTextWidth = 420f;
        private static readonly Vector2 Padding = new(48f, 34f);

        protected override void RefreshView()
        {
            var label = UI.Bubble_Label.TextMeshProUGUI;
            if (label.text != Model.Text)
            {
                label.richText = false;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.text = Model.Text;
                label.maxVisibleCharacters = int.MaxValue;
                Vector2 natural = label.GetPreferredValues(Model.Text);
                float width = Mathf.Clamp(natural.x, 96f, MaxTextWidth);
                Vector2 wrapped = label.GetPreferredValues(Model.Text, width, Mathf.Infinity);
                UI.Bubble.RectTransform.sizeDelta = new Vector2(width, Mathf.Max(38f, wrapped.y)) + Padding;
            }
            // Layout is based on the entire line, so words never move while typing.
            label.maxVisibleCharacters = Model.VisibleCharacters;
            UI.Bubble.RectTransform.anchoredPosition = Model.Position + Vector2.up * 24f;
            UI.Bubble.GameObject.SetActive(Model.Visible);
        }
    }
}
