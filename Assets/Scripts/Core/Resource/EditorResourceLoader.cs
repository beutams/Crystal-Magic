using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CrystalMagic.Core
{
    public class EditorResourceLoader : IResourceLoader
    {
        public void Initialize()
        {
        }

        public T Load<T>(string path) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<T>(path);
#else
            Debug.LogError("[EditorResourceLoader] Cannot use EditorResourceLoader outside editor!");
            return null;
#endif
        }

        public Sprite LoadSprite(string path, string spriteName)
        {
#if UNITY_EDITOR
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && string.Equals(sprite.name, spriteName, StringComparison.Ordinal))
                    return sprite;
            }

            return null;
#else
            Debug.LogError("[EditorResourceLoader] Cannot use EditorResourceLoader outside editor!");
            return null;
#endif
        }

        public System.Collections.IEnumerator LoadAsync<T>(string path, System.Action<T> onComplete) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            T asset = Load<T>(path);
            onComplete?.Invoke(asset);
            yield return null;
#else
            Debug.LogError("[EditorResourceLoader] Cannot use EditorResourceLoader outside editor!");
            yield break;
#endif
        }

        public void Release(string path)
        {
        }

        public void ReleaseAll()
        {
        }
    }
}
