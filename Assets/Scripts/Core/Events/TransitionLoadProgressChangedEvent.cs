namespace CrystalMagic.Core
{
    public readonly struct TransitionLoadProgressChangedEvent : IGameEvent
    {
        public TransitionLoadProgressChangedEvent(
            string targetSceneName,
            float progress,
            string title,
            string detail)
        {
            TargetSceneName = targetSceneName;
            Progress = progress;
            Title = title;
            Detail = detail;
        }

        public string TargetSceneName { get; }
        public float Progress { get; }
        public string Title { get; }
        public string Detail { get; }
    }
}
