using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class ProgressNotificationItem : NotificationItemView
    {
        public float GrowthSeconds = 0.55f;
        public AnimationCurve GrowthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        private float _shown, _from, _to, _elapsed;
        protected override float ContentDuration => Mathf.Max(0, GrowthSeconds);
        protected override void SetContent(bool fresh)
        {
            _from = fresh ? Mathf.Clamp(Request.From, 0, Request.Maximum) : _shown;
            _to = Mathf.Clamp(Request.To, 0, Request.Maximum);
            _elapsed = 0;
            _shown = _from;
            Render();
        }
        protected override void TickContent(float deltaTime)
        {
            _elapsed += deltaTime;
            float progress = GrowthSeconds > 0 ? Mathf.Clamp01(_elapsed / GrowthSeconds) : 1;
            _shown = Mathf.LerpUnclamped(_from, _to, GrowthCurve.Evaluate(progress));
            Render();
        }
        private void Render()
        {
            // Stretch a simple Image, so a sprite is optional and the bar can be reskinned freely.
            UI.Fill.RectTransform.anchorMax = new Vector2(Mathf.Clamp01(_shown / Request.Maximum), 1);
            UI.Label.TextMeshProUGUI.text = $"{Request.Text}  {Mathf.RoundToInt(_shown / Request.Maximum * 100)}%";
        }
    }
}
