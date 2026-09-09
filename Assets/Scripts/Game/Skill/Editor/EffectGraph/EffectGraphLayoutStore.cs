using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.EffectGraph
{
    [Serializable]
    public sealed class EffectGraphContainerLayout
    {
        public string Path;
        public Vector2 Position;
        public bool Expanded = true;
    }

    [Serializable]
    public sealed class EffectGraphLayoutData
    {
        public Vector2 ViewPosition;
        public float ViewScale = 1f;
        public List<EffectGraphContainerLayout> Containers = new();
    }

    [Serializable]
    internal sealed class EffectGraphLayoutEntry
    {
        public string OwnerKey;
        public EffectGraphLayoutData Layout = new();
    }

    [Serializable]
    internal sealed class EffectGraphLayoutDocument
    {
        public int Version = 1;
        public List<EffectGraphLayoutEntry> Entries = new();
    }

    public sealed class EffectGraphLayoutStore
    {
        public const string DefaultPath = "Assets/Editor/EffectGraphLayouts.json";

        private readonly string _path;

        public EffectGraphLayoutStore(string path = DefaultPath)
        {
            _path = string.IsNullOrWhiteSpace(path) ? DefaultPath : path.Replace('\\', '/');
        }

        public EffectGraphLayoutData Load(string ownerKey)
        {
            EffectGraphLayoutDocument document = ReadDocument();
            for (int index = 0; index < document.Entries.Count; index++)
            {
                EffectGraphLayoutEntry entry = document.Entries[index];
                if (entry != null && string.Equals(entry.OwnerKey, ownerKey, StringComparison.Ordinal))
                    return CloneLayout(entry.Layout);
            }

            return new EffectGraphLayoutData();
        }

        public void Save(string ownerKey, EffectGraphLayoutData layout)
        {
            if (string.IsNullOrWhiteSpace(ownerKey))
                return;

            EffectGraphLayoutDocument document = ReadDocument();
            EffectGraphLayoutEntry entry = null;
            for (int index = 0; index < document.Entries.Count; index++)
            {
                if (string.Equals(document.Entries[index]?.OwnerKey, ownerKey, StringComparison.Ordinal))
                {
                    entry = document.Entries[index];
                    break;
                }
            }

            if (entry == null)
            {
                entry = new EffectGraphLayoutEntry { OwnerKey = ownerKey };
                document.Entries.Add(entry);
            }

            entry.Layout = CloneLayout(layout);
            WriteDocument(document);
        }

        public void Prune(string ownerKey, ISet<string> validContainerPaths)
        {
            EffectGraphLayoutData layout = Load(ownerKey);
            layout.Containers.RemoveAll(container =>
                container == null || string.IsNullOrWhiteSpace(container.Path) ||
                validContainerPaths == null || !validContainerPaths.Contains(container.Path));
            Save(ownerKey, layout);
        }

        private EffectGraphLayoutDocument ReadDocument()
        {
            try
            {
                if (!File.Exists(_path))
                    return new EffectGraphLayoutDocument();

                EffectGraphLayoutDocument document = JsonUtility.FromJson<EffectGraphLayoutDocument>(File.ReadAllText(_path));
                document ??= new EffectGraphLayoutDocument();
                document.Entries ??= new List<EffectGraphLayoutEntry>();
                return document;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[EffectGraph] Ignored invalid layout file '{_path}': {exception.Message}");
                return new EffectGraphLayoutDocument();
            }
        }

        private void WriteDocument(EffectGraphLayoutDocument document)
        {
            try
            {
                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(_path, JsonUtility.ToJson(document, true));
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[EffectGraph] Could not save layout file '{_path}': {exception.Message}");
            }
        }

        private static EffectGraphLayoutData CloneLayout(EffectGraphLayoutData source)
        {
            EffectGraphLayoutData clone = new()
            {
                ViewPosition = source?.ViewPosition ?? Vector2.zero,
                ViewScale = Mathf.Max(0.1f, source?.ViewScale ?? 1f),
            };

            if (source?.Containers == null)
                return clone;

            for (int index = 0; index < source.Containers.Count; index++)
            {
                EffectGraphContainerLayout container = source.Containers[index];
                if (container == null || string.IsNullOrWhiteSpace(container.Path))
                    continue;

                clone.Containers.Add(new EffectGraphContainerLayout
                {
                    Path = container.Path,
                    Position = container.Position,
                    Expanded = container.Expanded,
                });
            }

            return clone;
        }
    }
}
