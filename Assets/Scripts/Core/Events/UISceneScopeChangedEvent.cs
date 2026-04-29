namespace CrystalMagic.Core
{
    public readonly struct UISceneScopeChangedEvent : IGameEvent
    {
        public UISceneScopeChangedEvent(string sceneName)
        {
            SceneName = sceneName;
        }

        public string SceneName { get; }
    }
}
