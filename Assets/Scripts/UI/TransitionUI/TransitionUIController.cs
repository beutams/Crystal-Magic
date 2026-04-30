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
        }
    }
}
