using System;
using CrystalMagic.Core;
using UnityEngine;

namespace CrystalMagic.UI
{
    [Serializable]
    public sealed class NotificationMotion
    {
        public float EnterSeconds = 0.15f, HoldSeconds = 1.2f, ExitSeconds = 0.4f;
        public Vector2 Position, StackSpacing = new(0, 32), EnterOffset = new(0, -8), HoldOffset, ExitOffset;
        public float EnterScale = 0.95f, ExitScale = 1f;
        public bool FadeDuringHold;
        public AnimationCurve EnterCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve HoldCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve ExitCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    }

    public abstract class NotificationItemView : UISubView<NotificationItemData>
    {
        public NotificationMotion Motion = new();
        private int _revision = -1, _id;
        private float _elapsed, _contentDuration;
        private Color _prefabTint;
        private bool _cachedTint;
        protected NotificationRequest Request { get; private set; }
        protected virtual float ContentDuration => 0;

        public void Present(NotificationRecord record, int slot)
        {
            _position = Motion.Position + Motion.StackSpacing * slot;
            if (record.Id == _id && record.Revision == _revision) { ApplyMotion(); return; }
            bool fresh = record.Id != _id;
            if (!_cachedTint) { _prefabTint = UI.Label.TextMeshProUGUI.color; _cachedTint = true; }
            _id = record.Id; _revision = record.Revision; Request = record.Request;
            UI.Label.TextMeshProUGUI.color = _prefabTint * Request.Tint;
            UI.Label.TextMeshProUGUI.text = Request.Text;
            SetContent(fresh);
            _contentDuration = ContentDuration;
            _elapsed = fresh ? 0 : Mathf.Max(0, Motion.EnterSeconds);
            ApplyMotion();
        }

        private Vector2 _position;
        public bool Tick(float deltaTime)
        {
            deltaTime = Mathf.Max(0, deltaTime);
            float before = Mathf.Max(0, _elapsed - Mathf.Max(0, Motion.EnterSeconds));
            _elapsed += deltaTime;
            float after = Mathf.Max(0, _elapsed - Mathf.Max(0, Motion.EnterSeconds));
            TickContent(after - before);
            ApplyMotion();
            return _elapsed >= Mathf.Max(0, Motion.EnterSeconds) + _contentDuration +
                Mathf.Max(0, Motion.HoldSeconds) + Mathf.Max(0, Motion.ExitSeconds);
        }

        private void ApplyMotion()
        {
            float enter = Mathf.Max(0, Motion.EnterSeconds);
            float hold = _contentDuration + Mathf.Max(0, Motion.HoldSeconds);
            Vector2 offset;
            float alpha, scale;
            if (_elapsed < enter)
            {
                float t = Motion.EnterCurve.Evaluate(_elapsed / enter);
                offset = Vector2.LerpUnclamped(Motion.EnterOffset, Vector2.zero, t);
                alpha = t; scale = Mathf.LerpUnclamped(Motion.EnterScale, 1, t);
            }
            else if (_elapsed < enter + hold)
            {
                float progress = hold > 0 ? (_elapsed - enter) / hold : 1;
                offset = Motion.HoldOffset * Motion.HoldCurve.Evaluate(progress);
                alpha = Motion.FadeDuringHold ? 1 - progress : 1; scale = 1;
            }
            else
            {
                float progress = Motion.ExitSeconds > 0 ? Mathf.Clamp01((_elapsed - enter - hold) / Motion.ExitSeconds) : 1;
                float t = Motion.ExitCurve.Evaluate(progress);
                offset = Motion.HoldOffset + Motion.ExitOffset * t;
                alpha = Motion.FadeDuringHold ? 0 : 1 - t;
                scale = Mathf.LerpUnclamped(1, Motion.ExitScale, t);
            }
            UI.Root.RectTransform.anchoredPosition = _position + offset;
            UI.Root.RectTransform.localScale = Vector3.one * scale;
            UI.Root.CanvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void ResetForPool()
        {
            _id = 0; _revision = -1; _elapsed = 0;
            UI.Root.CanvasGroup.alpha = 0;
            UI.Root.RectTransform.localScale = Vector3.one;
            UI.Label.TextMeshProUGUI.text = string.Empty;
        }

        protected virtual void SetContent(bool fresh) { }
        protected virtual void TickContent(float deltaTime) { }
    }
}
