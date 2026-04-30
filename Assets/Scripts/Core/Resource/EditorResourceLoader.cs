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

        public T Load<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<T>(path);
#else
            Debug.LogError("[EditorResourceLoader] Cannot use EditorResourceLoader outside editor!");
            return null;
#endif
        }

        public System.Collections.IEnumerator LoadAsync<T>(string path, System.Action<T> onComplete) where T : Object
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
