using System;
using System.Collections.Generic;
using System.IO;
using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Core
{
    public static class LocalizationEditorPreviewUtility
    {
        private static string TablePath => Path.Combine(Application.dataPath, "Res", "Data", "LocalizationDataTable.json");

        private static readonly Dictionary<string, LocalizationData> Entries = new(StringComparer.Ordinal);
        private static DateTime _lastWriteUtc = DateTime.MinValue;

        public static string ResolveChineseSimplified(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key))
                return fallback;

            EnsureLoaded();
            if (!Entries.TryGetValue(key, out LocalizationData entry))
                return fallback;

            return string.IsNullOrEmpty(entry.ChineseSimplified)
                ? fallback
                : entry.ChineseSimplified;
        }

        public static bool ContainsKey(string key)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(key) && Entries.ContainsKey(key);
        }

        private static void EnsureLoaded()
        {
            if (!File.Exists(TablePath))
            {
                Entries.Clear();
                return;
            }

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(TablePath);
            if (lastWriteUtc == _lastWriteUtc)
                return;

            _lastWriteUtc = lastWriteUtc;
            Entries.Clear();

            try
            {
                string json = DataFileUtility.ReadJsonText(TablePath);
                LocalizationTable table = JsonUtility.FromJson<LocalizationTable>(json);
                if (table?.Rows == null)
                    return;

                foreach (LocalizationData entry in table.Rows)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Key))
                        Entries[entry.Key] = entry;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LocalizationEditorPreview] Failed to load {TablePath}: {exception.Message}");
            }
        }

        [Serializable]
        private sealed class LocalizationTable
        {
            public LocalizationData[] Rows = Array.Empty<LocalizationData>();
        }
    }
}
