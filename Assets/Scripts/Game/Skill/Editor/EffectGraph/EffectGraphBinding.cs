using System;
using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Editor.EffectGraph
{
    public sealed class EffectGraphBinding
    {
        private readonly Func<EffectData[]> _getRootEffects;
        private readonly Action<EffectData[]> _setRootEffects;
        private readonly Action _notifyChanged;

        public EffectGraphBinding(
            string ownerKey,
            string displayName,
            Func<EffectData[]> getRootEffects,
            Action<EffectData[]> setRootEffects,
            Action notifyChanged)
        {
            OwnerKey = ownerKey ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            _getRootEffects = getRootEffects ?? throw new ArgumentNullException(nameof(getRootEffects));
            _setRootEffects = setRootEffects ?? throw new ArgumentNullException(nameof(setRootEffects));
            _notifyChanged = notifyChanged ?? throw new ArgumentNullException(nameof(notifyChanged));
        }

        public string OwnerKey { get; }

        public string DisplayName { get; }

        public EffectData[] GetRootEffects()
        {
            return _getRootEffects() ?? Array.Empty<EffectData>();
        }

        public void SetRootEffects(EffectData[] effects)
        {
            _setRootEffects(effects ?? Array.Empty<EffectData>());
        }

        public void NotifyChanged()
        {
            _notifyChanged();
        }
    }
}
