using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    [Serializable]
    internal sealed class NPCInteractionGraphNodeLayout
    {
        public string Guid;
        public Vector2 Position;
    }

    [Serializable]
    internal sealed class NPCInteractionGraphLayoutData
    {
        public Vector2 ViewPosition;
        public float ViewScale = 1f;
        public List<NPCInteractionGraphNodeLayout> Nodes = new();
    }

    [Serializable]
    internal sealed class NPCInteractionGraphLayoutEntry
    {
        public string OwnerKey;
        public NPCInteractionGraphLayoutData Layout = new();
    }

    [Serializable]
    internal sealed class NPCInteractionGraphLayoutDocument
    {
        public int Version = 1;
        public List<NPCInteractionGraphLayoutEntry> Entries = new();
    }

    internal sealed class NPCInteractionGraphLayoutStore
    {
        private const string DefaultPath = "Assets/Scripts/Game/Data/Editor/NPCInteractionGraphLayouts.json";

        public NPCInteractionGraphLayoutData Load(string ownerKey)
        {
            NPCInteractionGraphLayoutDocument document = ReadDocument();
            for (int index = 0; index < document.Entries.Count; index++)
            {
                NPCInteractionGraphLayoutEntry entry = document.Entries[index];
                if (entry != null && string.Equals(entry.OwnerKey, ownerKey, StringComparison.Ordinal))
                    return Clone(entry.Layout);
            }

            return new NPCInteractionGraphLayoutData();
        }

        public void Save(string ownerKey, NPCInteractionGraphLayoutData layout, ISet<string> validNodeGuids)
        {
            if (string.IsNullOrWhiteSpace(ownerKey))
                return;

            NPCInteractionGraphLayoutDocument document = ReadDocument();
            NPCInteractionGraphLayoutEntry entry = null;
            for (int index = 0; index < document.Entries.Count; index++)
            {
                NPCInteractionGraphLayoutEntry candidate = document.Entries[index];
                if (candidate != null && string.Equals(candidate.OwnerKey, ownerKey, StringComparison.Ordinal))
                {
                    entry = candidate;
                    break;
                }
            }

            if (entry == null)
            {
                entry = new NPCInteractionGraphLayoutEntry { OwnerKey = ownerKey };
                document.Entries.Add(entry);
            }

            entry.Layout = Clone(layout);
            entry.Layout.Nodes.RemoveAll(node =>
                node == null || string.IsNullOrWhiteSpace(node.Guid) ||
                validNodeGuids == null || !validNodeGuids.Contains(node.Guid));
            WriteDocument(document);
        }

        private static NPCInteractionGraphLayoutDocument ReadDocument()
        {
            try
            {
                if (!File.Exists(DefaultPath))
                    return new NPCInteractionGraphLayoutDocument();

                NPCInteractionGraphLayoutDocument document = JsonUtility.FromJson<NPCInteractionGraphLayoutDocument>(File.ReadAllText(DefaultPath));
                document ??= new NPCInteractionGraphLayoutDocument();
                document.Entries ??= new List<NPCInteractionGraphLayoutEntry>();
                return document;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[NPCInteractionGraph] Ignored invalid layout file '{DefaultPath}': {exception.Message}");
                return new NPCInteractionGraphLayoutDocument();
            }
        }

        private static void WriteDocument(NPCInteractionGraphLayoutDocument document)
        {
            try
            {
                string directory = Path.GetDirectoryName(DefaultPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(DefaultPath, JsonUtility.ToJson(document, true));
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[NPCInteractionGraph] Could not save layout file '{DefaultPath}': {exception.Message}");
            }
        }

        private static NPCInteractionGraphLayoutData Clone(NPCInteractionGraphLayoutData source)
        {
            NPCInteractionGraphLayoutData clone = new()
            {
                ViewPosition = source?.ViewPosition ?? Vector2.zero,
                ViewScale = Mathf.Max(0.1f, source?.ViewScale ?? 1f),
            };

            if (source?.Nodes == null)
                return clone;

            for (int index = 0; index < source.Nodes.Count; index++)
            {
                NPCInteractionGraphNodeLayout node = source.Nodes[index];
                if (node == null || string.IsNullOrWhiteSpace(node.Guid))
                    continue;

                clone.Nodes.Add(new NPCInteractionGraphNodeLayout
                {
                    Guid = node.Guid,
                    Position = node.Position,
                });
            }

            return clone;
        }
    }
}
