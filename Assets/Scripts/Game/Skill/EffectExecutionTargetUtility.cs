using CrystalMagic.Game.Data.Effects;

namespace CrystalMagic.Game.Skill
{
    public static class EffectExecutionTargetUtility
    {
        public static bool IsPresentation(EffectData effect) => effect is SpawnVfxEffectData or SpawnFollowVfxEffectData or
            SpawnLineVfxEffectData or MoveVfxEffectData or SpawnSoundEffectData or CameraShakeEffectData;

        public static GameWorldExecutionTarget DefaultTargets(EffectData effect) => IsPresentation(effect)
            ? GameWorldExecutionTarget.Standalone | GameWorldExecutionTarget.Client : SupportedTargets(effect);
        // A new result-changing effect is authority-only until explicitly made safe for prediction.
        public static GameWorldExecutionTarget SupportedTargets(EffectData effect) => effect switch
        {
            SpawnProjectileEffectData or SpawnVfxEffectData or SpawnFollowVfxEffectData or
            SpawnLineVfxEffectData or MoveVfxEffectData or SpawnSoundEffectData or CameraShakeEffectData or
            AreaSearchEffectData or ChainSearchEffectData or ConeSearchEffectData or
            ForwardRectSearchEffectData or RectSearchEffectData or ReadBuffStackEffectData or
            PersistentEffectData => GameWorldExecutionTarget.All,
            _ => GameWorldExecutionTarget.Standalone | GameWorldExecutionTarget.Server,
        };

        public static bool CanExecute(EffectData effect, GameWorldRole role)
        {
            // The authority publishes client presentation nodes; it never creates their renderers.
            if (role == GameWorldRole.Server && IsPresentation(effect))
                return (effect.ExecutionTargets & GameWorldExecutionTarget.Client) != 0;
            return GameWorldExecutionTargetUtility.Contains(effect.ExecutionTargets & SupportedTargets(effect), role);
        }
    }
}
