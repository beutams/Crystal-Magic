using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class GuideUIOpenData
    {
        public NPCInteractionSession Session;
        public NPCGuideInteractionNodeData Node;
    }

    public sealed class GuideUIModel : UIModelBase, IUIOpenDataReceiver<GuideUIOpenData>
    {
        public override string ChangedEventName => "GuideUI.Changed";
        public NPCInteractionSession Session { get; private set; }
        public NPCGuideInteractionNodeData Node { get; private set; }
        public Rect[] Holes { get; private set; } = System.Array.Empty<Rect>();
        public string Text { get; private set; } = "";
        public bool BlockOutside { get; private set; }
        public void SetOpenData(GuideUIOpenData data) { Session = data.Session; Node = data.Node; }
        public void SetPresentation(Rect[] holes, bool block, string text)
        {
            bool same = Holes.Length == holes.Length && BlockOutside == block && Text == text;
            for (int i = 0; same && i < holes.Length; i++) same = Holes[i] == holes[i];
            if (same) return;
            Holes = holes; BlockOutside = block; Text = text;
            EventComponent.Instance.Publish(new CommonGameEvent(ChangedEventName, this));
        }
        public override void Dispose() { Session = null; Node = null; Holes = System.Array.Empty<Rect>(); }
    }
}
