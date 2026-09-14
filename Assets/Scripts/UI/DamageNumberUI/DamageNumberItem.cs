using TMPro;
using UnityEngine;

namespace CrystalMagic.UI
{
    [DisallowMultipleComponent]
    public sealed class DamageNumberItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _label;

        [SerializeField]
        private CanvasGroup _canvasGroup;

        private RectTransform _rectTransform;
        private Vector2 _startPosition;
        private Vector2 _drift;
        private float _elapsed;
        private float _duration;

        private void Awake()
        {
            _rectTransform = transform as RectTransform;
        }

        public void Show(float amount, Vector2 anchoredPosition, Vector2 drift, float duration)
        {
            _startPosition = anchoredPosition;
            _drift = drift;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, duration);

            _rectTransform.anchoredPosition = _startPosition;
            _rectTransform.localScale = Vector3.one * 1.15f;
            _canvasGroup.alpha = 1f;
            _label.text = $"-{amount:0.#}";
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            float progress = Mathf.Clamp01(_elapsed / _duration);
            float easedProgress = 1f - (1f - progress) * (1f - progress);

            _rectTransform.anchoredPosition = _startPosition + _drift * easedProgress;
            _rectTransform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, easedProgress);
            _canvasGroup.alpha = 1f - progress;
            return progress >= 1f;
        }
    }
}
