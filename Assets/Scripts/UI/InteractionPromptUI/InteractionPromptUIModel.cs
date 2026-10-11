using CrystalMagic.Core;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class InteractionPromptUIModel : UIModelBase
    {
        public override string ChangedEventName => "InteractionPromptUIModel.Changed";
        public string Text { get; private set; } = string.Empty;
        public Vector2 Position { get; private set; }
        public bool Visible { get; private set; }
        public bool AboveAnchor { get; private set; }

        public void SetDisplay(string text, Vector2 position, bool visible, bool aboveAnchor = false)
        {
            if (Text == text && Position == position && Visible == visible && AboveAnchor == aboveAnchor)
                return;

            Text = text;
            Position = position;
            Visible = visible;
            AboveAnchor = aboveAnchor;
            EventComponent.Instance.Publish(new CommonGameEvent(ChangedEventName, this));
        }

        public void SetVisible(bool visible) => SetDisplay(Text, Position, visible, AboveAnchor);
    }
}
