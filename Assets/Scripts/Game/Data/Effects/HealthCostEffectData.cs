namespace CrystalMagic.Game.Data.Effects
{
    [System.Serializable]
    public sealed class HealthCostEffectData : EffectData
    {
        [EditorLabel("最大生命系数")]
        public float MaxHealthCoefficient;

        [EditorLabel("额外扣血")]
        public float FlatHealthCost;
    }
}
