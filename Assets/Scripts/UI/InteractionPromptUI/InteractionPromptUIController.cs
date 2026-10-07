using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class InteractionPromptUIController : UIControllerBase<InteractionPromptUI, InteractionPromptUIModel>
    {
        public InteractionPromptUIController(InteractionPromptUI view, InteractionPromptUIModel model) : base(view, model) { }

        protected override void OnOpen() => View.BindModel(Model);
    }
}
