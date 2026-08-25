namespace CrystalMagic.UI
{
    public sealed class LobbyUIController : UIControllerBase<LobbyUI, LobbyUIModel>
    {
        public LobbyUIController(LobbyUI view, LobbyUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
        }
    }
}
