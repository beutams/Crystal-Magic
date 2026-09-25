using TMPro;
using UnityEngine;

namespace CrystalMagic.UI
{
    [DisallowMultipleComponent]
    public sealed class PickupTipItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _label;

        [SerializeField]
        private CanvasGroup _canvasGroup;

        private RectTransform _rectTransform;
        private Vector2 _startPosition;
        private float _elapsed;
        private float _duration;

        private void Awake()
        {
            _rectTransform = transform as RectTransform;
        }

        public void Show(string text, Color color, Vector2 anchoredPosition, float duration)
        {
            _startPosition = anchoredPosition;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, duration);
            _rectTransform.anchoredPosition = _startPosition;
            _rectTransform.localScale = Vector3.one;
            _canvasGroup.alpha = 1f;
            _label.color = color;
            _label.text = text;
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            float progress = Mathf.Clamp01(_elapsed / _duration);
            float eased = 1f - (1f - progress) * (1f - progress);
            _rectTransform.anchoredPosition = _startPosition + Vector2.up * (72f * eased);
            _canvasGroup.alpha = 1f - progress;
            return progress >= 1f;
        }
    }
}
