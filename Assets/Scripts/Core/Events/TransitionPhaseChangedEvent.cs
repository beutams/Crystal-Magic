namespace CrystalMagic.Core
{
    public enum TransitionPhase
    {
        FadeInStarted,
        FadeInCompleted,
        LoadStarted,
        LoadCompleted,
        FadeOutStarted,
        FadeOutCompleted,
    }

    public readonly struct TransitionPhaseChangedEvent : IGameEvent
    {
        public TransitionPhaseChangedEvent(TransitionPhase phase, string targetSceneName, float progress = 0f)
        {
            Phase = phase;
            TargetSceneName = targetSceneName;
            Progress = progress;
        }

        public TransitionPhase Phase { get; }
        public string TargetSceneName { get; }
        public float Progress { get; }
    }
}
