using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CrystalMagic.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class UIResourceConfigTests
{
    [Test]
    public void RuntimeResourceConfigCreatesFloatingAndTransitionGroups()
    {
        GameObject root = new("Runtime UI config fixture");
        TextAsset config = new(UIConfigLoader.SaveToJson(new UIGroupConfig
        {
            groups = new List<UIGroupEntry>
            {
                new() { groupName = "Bottom", groupType = UIGroupType.List, order = 80,
                    uiNames = new List<string> { "UnitHealthBarUI", "InteractionPromptUI" } },
                new() { groupName = "Top", groupType = UIGroupType.List, order = 10000,
                    uiNames = new List<string> { "TransitionUI" } },
                new() { groupName = "ResourceOnly", groupType = UIGroupType.List, order = 81 },
            },
        }));
        try
        {
            ResourceComponent resource = root.AddComponent<ResourceComponent>();
            ConfigLoader loader = new(config);
            typeof(ResourceComponent).GetField("_loader", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(resource, loader);
            UIComponent ui = root.AddComponent<UIComponent>();
            typeof(UIComponent).GetMethod("LoadConfigFromPath", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(ui, null);

            Assert.That(loader.LoadCount, Is.EqualTo(1));
            Assert.That(loader.ReleaseCount, Is.EqualTo(1));
            Assert.That(ui.GetGroup<UIGroup>("Bottom").BaseSortingOrder, Is.EqualTo(80));
            Assert.That(ui.GetGroup<UIGroup>("Top").BaseSortingOrder, Is.EqualTo(10000));
            Assert.That(ui.GetGroup<UIGroup>("ResourceOnly"), Is.Not.Null);
            Assert.That(ui.GetGroupNameByUIName("UnitHealthBarUI"), Is.EqualTo("Bottom"));
            Assert.That(ui.GetGroupNameByUIName("InteractionPromptUI"), Is.EqualTo("Bottom"));
            Assert.That(ui.GetGroupNameByUIName("TransitionUI"), Is.EqualTo("Top"));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }
    }

    private sealed class ConfigLoader : IResourceLoader
    {
        private const string Path = "Assets/Res/Config/ui_config.json";
        private readonly TextAsset _config;
        public int LoadCount;
        public int ReleaseCount;

        public ConfigLoader(TextAsset config) => _config = config;
        public void Initialize() { }
        public T Load<T>(string path) where T : Object
        {
            Assert.That(path, Is.EqualTo(Path));
            LoadCount++;
            return _config as T;
        }
        public Sprite LoadSprite(string path, string spriteName) => null;
        public IEnumerator LoadAsync<T>(string path, System.Action<T> onComplete) where T : Object
        {
            onComplete(Load<T>(path));
            yield break;
        }
        public void Release(string path)
        {
            Assert.That(path, Is.EqualTo(Path));
            ReleaseCount++;
        }
        public void ReleaseAll() { }
    }
}
