using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// Standard damage resolution for Buff triggers without creating another
    /// OnDamaged event and recursively triggering reactive Buffs.
    /// </summary>
    public sealed class BuffDamageEffect : DamageEffect
    {
        public BuffDamageEffect(BuffDamageEffectData data)
            : base(data)
        {
        }

        protected override bool SendsOnDamagedHook => false;
    }
}
