using System;
using CrystalMagic.Core;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class DialogueUIOpenData
    {
        public World World;
        public Entity Anchor;
        public string Speaker;
        public DialoguePlayback Playback;
        public float WorldYOffset = 1.35f;
        public Action OnClosed;
    }

    public sealed class DialogueUIModel : UIModelBase, IUIOpenDataReceiver<DialogueUIOpenData>
    {
        public override string ChangedEventName => "DialogueUI.Changed";
        public World World { get; private set; }
        public Entity Anchor { get; private set; }
        public string Text { get; private set; } = string.Empty;
        public DialoguePlayback Playback { get; private set; }
        public float WorldYOffset { get; private set; }
        public Vector2 Position { get; private set; }
        public bool Visible { get; private set; }
        public int VisibleCharacters { get; private set; }
        private int _prefixCharacters;
        private Action _onClosed;

        public void SetOpenData(DialogueUIOpenData data)
        {
            World = data.World;
            Anchor = data.Anchor;
            Playback = data.Playback;
            WorldYOffset = float.IsNaN(data.WorldYOffset) || float.IsInfinity(data.WorldYOffset)
                ? 1.35f : Mathf.Max(0f, data.WorldYOffset);
            _onClosed = data.OnClosed;
            string prefix = string.IsNullOrWhiteSpace(data.Speaker) ? string.Empty : data.Speaker + "\n";
            var prefixPlayback = new DialoguePlayback(prefix, 120f);
            prefixPlayback.Advance(float.MaxValue);
            _prefixCharacters = prefixPlayback.VisibleCharacters;
            Text = prefixPlayback.Text + Playback.Text;
            VisibleCharacters = _prefixCharacters + Playback.VisibleCharacters;
            Visible = false;
        }

        public void Advance(float deltaTime)
        {
            Playback.Advance(deltaTime);
            int count = _prefixCharacters + Playback.VisibleCharacters;
            if (count == VisibleCharacters)
                return;
            VisibleCharacters = count;
            NotifyChanged();
        }

        public void SetAnchor(Vector2 position, bool visible)
        {
            if (Position == position && Visible == visible)
                return;
            Position = position;
            Visible = visible;
            NotifyChanged();
        }

        private void NotifyChanged() => EventComponent.Instance.Publish(new CommonGameEvent(ChangedEventName, this));

        public void ClosePresentation()
        {
            if (Playback != null && !Playback.IsCompleted)
                Playback.Cancel();
            Action closed = _onClosed;
            _onClosed = null;
            closed?.Invoke();
        }

        public override void Dispose()
        {
            ClosePresentation();
            World = null;
            Playback = null;
            Text = string.Empty;
        }
    }
}
