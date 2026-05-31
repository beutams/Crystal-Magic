using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class TrainingDummyStatsUIController : UIControllerBase<TrainingDummyStatsUI, TrainingDummyStatsUIModel>
    {
        public TrainingDummyStatsUIController(TrainingDummyStatsUI view, TrainingDummyStatsUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            BindEvent<UnitDamagedEvent>(Model.HandleUnitDamaged);
            Model.RefreshRuntime();
        }

        protected override void OnUpdate()
        {
            Model.RefreshRuntime();
        }
    }
}
