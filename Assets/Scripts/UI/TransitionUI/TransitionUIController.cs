using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class TransitionUIController : UIControllerBase<TransitionUI, TransitionUIModel>
    {
        public TransitionUIController(TransitionUI view, TransitionUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            if (EventComponent.Instance == null)
                return;

            EventComponent.Instance.Subscribe<TransitionLoadProgressChangedEvent>(HandleTransitionLoadProgressChanged);
            EventComponent.Instance.Subscribe<TransitionPhaseChangedEvent>(HandleTransitionPhaseChanged);
        }

        protected override void OnClose()
        {
            if (EventComponent.Instance == null)
                return;

            EventComponent.Instance.Unsubscribe<TransitionLoadProgressChangedEvent>(HandleTransitionLoadProgressChanged);
            EventComponent.Instance.Unsubscribe<TransitionPhaseChangedEvent>(HandleTransitionPhaseChanged);
        }

        private void HandleTransitionLoadProgressChanged(TransitionLoadProgressChangedEvent gameEvent)
        {
            View.SetStatus(gameEvent.Title, gameEvent.Detail, gameEvent.Progress);
        }

        private void HandleTransitionPhaseChanged(TransitionPhaseChangedEvent gameEvent)
        {
            switch (gameEvent.Phase)
            {
                case TransitionPhase.LoadStarted:
                    View.SetStatus("Loading", gameEvent.TargetSceneName, gameEvent.Progress);
                    break;
                case TransitionPhase.LoadCompleted:
                    View.SetStatus("Load complete", gameEvent.TargetSceneName, 1f);
                    break;
                case TransitionPhase.FadeOutStarted:
                    View.SetStatus("Entering scene", gameEvent.TargetSceneName, 1f);
                    break;
            }
        }
    }
}
