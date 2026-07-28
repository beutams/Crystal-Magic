using System;
using System.Collections.Generic;
using System.Globalization;
using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Core
{
    public sealed class LocalizationComponent : GameComponent<LocalizationComponent>
    {
        public const string LanguageChangedEventName = "Localization.LanguageChanged";

        private readonly Dictionary<string, LocalizationData> _entries = new(StringComparer.Ordinal);

        public static event Action LanguageChanged;

        public GameLanguage CurrentLanguage { get; private set; } = GameLanguage.ChineseSimplified;

        public override int Priority => 31;

        public override void Initialize()
        {
            base.Initialize();

            LoadEntries();
            ApplyLanguage(GameSettingsComponent.Instance.GetSettingsCopy().Language, true);
            EventComponent.Instance.Subscribe(
                new CommonGameEvent(GameSettingsComponent.SettingsChangedEventName),
                OnSettingsChanged);
        }

        public override void Cleanup()
        {
            EventComponent.Instance.Unsubscribe(
                new CommonGameEvent(GameSettingsComponent.SettingsChangedEventName),
                OnSettingsChanged);
            _entries.Clear();
            LanguageChanged = null;
            base.Cleanup();
        }

        public string Get(string key)
        {
            return TryGet(key, out string value) ? value : key ?? string.Empty;
        }

        public static string Resolve(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                return LocalizationEditorPreviewUtility.ResolveChineseSimplified(key, key);
#endif

            return TryGetInstance(out LocalizationComponent localization)
                ? localization.Get(key)
                : key;
        }

        public bool TryGet(string key, out string value)
        {
            value = key;
            if (string.IsNullOrEmpty(key) || !_entries.TryGetValue(key, out LocalizationData entry))
                return false;

            value = CurrentLanguage == GameLanguage.English
                ? entry.English
                : entry.ChineseSimplified;

            if (string.IsNullOrEmpty(value))
            {
                value = CurrentLanguage == GameLanguage.English
                    ? entry.ChineseSimplified
                    : entry.English;
            }

            return !string.IsNullOrEmpty(value);
        }

        public string Format(string key, params object[] arguments)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
        }

        public void SetLanguage(GameLanguage language, bool saveToDisk = true)
        {
            GameSettingsComponent.Instance.SetLanguage(language, saveToDisk);
        }

        private void LoadEntries()
        {
            _entries.Clear();

            DataTable<LocalizationData> table = DataComponent.Instance.GetTable<LocalizationData>();
            if (table == null)
            {
                Debug.LogError("[LocalizationComponent] LocalizationDataTable is not loaded.");
                return;
            }

            foreach (LocalizationData entry in table.GetAll())
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    Debug.LogWarning($"[LocalizationComponent] Localization row {entry.Id} has an empty key.");
                    continue;
                }

                if (!_entries.TryAdd(entry.Key, entry))
                    Debug.LogError($"[LocalizationComponent] Duplicate localization key: {entry.Key}");
            }
        }

        private void OnSettingsChanged(CommonGameEvent gameEvent)
        {
            GameSettingsData settings = gameEvent.GetData<GameSettingsData>();
            if (settings != null)
                ApplyLanguage(settings.Language, true);
        }

        private void ApplyLanguage(GameLanguage language, bool notify)
        {
            if (!Enum.IsDefined(typeof(GameLanguage), language))
                language = GameLanguage.ChineseSimplified;

            bool changed = CurrentLanguage != language;
            CurrentLanguage = language;

            if (!notify && !changed)
                return;

            LanguageChanged?.Invoke();
            EventComponent.Instance.Publish(new CommonGameEvent(LanguageChangedEventName, CurrentLanguage));
        }
    }
}
