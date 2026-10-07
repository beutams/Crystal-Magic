using System;
using UnityEngine;

namespace CrystalMagic.Core
{
    // Owned by SceneComponent, not a second global component. One presentation
    // lease covers the complete map, including procedural children and assets.
    public sealed class SceneMapPresentation
    {
        private Action _release;
        public string SceneName { get; private set; }
        public GameObject Root { get; private set; }

        public bool IsCurrent(GameObject root) => root != null && Root == root;

        public void Replace(string sceneName, GameObject root, Action release)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (release == null) throw new ArgumentNullException(nameof(release));
            if (Root == root) throw new InvalidOperationException("Map is already registered.");
            Clear();
            SceneName = sceneName;
            Root = root;
            _release = release;
        }

        public void Clear()
        {
            GameObject root = Root;
            Action release = _release;
            // Invalidate before callbacks; late coroutine/finally/OnDestroy work
            // must not retain ownership of the next map.
            Root = null;
            SceneName = null;
            _release = null;
            if (root != null) root.SetActive(false);
            release?.Invoke();
        }

        public void ClearIfCurrent(GameObject root)
        {
            if (IsCurrent(root)) Clear();
        }
    }
}
