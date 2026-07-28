using TMPro;
using UnityEngine;

namespace CrystalMagic.Core
{
    [ExecuteAlways]
    [AddComponentMenu("UI/Localized TextMeshProUGUI")]
    public sealed class LocalizedTextMeshProUGUI : TextMeshProUGUI
    {
        [SerializeField] private string _localizationKey = string.Empty;

        private bool _isLanguageChangeSubscribed;

        public string LocalizationKey
        {
            get => _localizationKey;
            set
            {
                string normalizedKey = value ?? string.Empty;
                if (_localizationKey == normalizedKey)
                    return;

                _localizationKey = normalizedKey;
                ApplyLocalizationKey();
            }
        }

        public override string text
        {
            get => base.text;
            set
            {
                _localizationKey = string.Empty;
                base.text = value;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SubscribeLanguageChange();
            ApplyLocalizationKey();
        }

        protected override void OnDisable()
        {
            UnsubscribeLanguageChange();
            base.OnDisable();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            ApplyLocalizationKey();
        }

        public void ApplyLocalizationKey()
        {
            if (!string.IsNullOrEmpty(_localizationKey))
                base.text = LocalizationComponent.Resolve(_localizationKey);

            SetAllDirty();
        }

        private void SubscribeLanguageChange()
        {
            if (_isLanguageChangeSubscribed)
                return;

            LocalizationComponent.LanguageChanged += OnLanguageChanged;
            _isLanguageChangeSubscribed = true;
        }

        private void UnsubscribeLanguageChange()
        {
            if (!_isLanguageChangeSubscribed)
                return;

            LocalizationComponent.LanguageChanged -= OnLanguageChanged;
            _isLanguageChangeSubscribed = false;
        }

        private void OnLanguageChanged()
        {
            ApplyLocalizationKey();
        }
    }
}
