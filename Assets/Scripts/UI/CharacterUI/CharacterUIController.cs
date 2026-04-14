namespace CrystalMagic.UI
{
    public sealed class CharacterUIController : UIControllerBase<CharacterUI, CharacterUIModel>
    {
        public CharacterUIController(CharacterUI view, CharacterUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
        }
    }
}
