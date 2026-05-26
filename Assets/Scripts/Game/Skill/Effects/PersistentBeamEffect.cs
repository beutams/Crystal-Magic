using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Skill.Effects
{
    public sealed class PersistentBeamEffect : Effect
    {
        public new PersistentBeamEffectData Data { get; }

        public PersistentBeamEffect(PersistentBeamEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            PersistentBeamEffectSystem system = PersistentBeamEffectSystem.Default;
            if (system == null || Data == null || context == null)
                return;

            system.AddEffect(Data, context);
        }
    }
}
