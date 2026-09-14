using UnityEngine;

namespace CrystalMagic.Core
{
    public interface IResourceLoader
    {
        void Initialize();

        T Load<T>(string path) where T : Object;

        Sprite LoadSprite(string path, string spriteName);

        System.Collections.IEnumerator LoadAsync<T>(string path, System.Action<T> onComplete) where T : Object;

        void Release(string path);

        void ReleaseAll();
    }
}
