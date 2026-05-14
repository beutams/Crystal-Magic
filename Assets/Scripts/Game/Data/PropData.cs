using CrystalMagic.Core;
using CrystalMagic.Game.Data.Effects;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    public enum PropTargetType
    {
        [EditorLabel("自身")]
        Self = 0,
        [EditorLabel("当前目标")]
        CurrentTarget = 1,
        [EditorLabel("目标位置")]
        TargetPosition = 2,
    }

    [System.Serializable]
    [ReadOnlyData]
    public sealed class PropData : DataRow
    {
        public string Name;
        public string Description;
        public PropTargetType TargetType;

        [SerializeReference]
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();
    }
}
