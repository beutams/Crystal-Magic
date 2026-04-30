namespace CrystalMagic.Core
{
    public interface ITransitionLoadingUI
    {
        void BindTransitionData(TransitionData transitionData);
        void RefreshTransitionPhase(TransitionPhase phase, float progress);
    }
}
