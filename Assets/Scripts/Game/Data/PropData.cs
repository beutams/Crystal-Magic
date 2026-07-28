using CrystalMagic.Core;
using CrystalMagic.Game.Data.Effects;
using Newtonsoft.Json;
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
        public string NameKey;
        public string DescriptionKey;
        public PropTargetType TargetType;

        [JsonIgnore]
        public string Name => LocalizationComponent.Resolve(NameKey);

        [JsonIgnore]
        public string Description => LocalizationComponent.Resolve(DescriptionKey);

        [EditorLabel("携带上限")]
        public int CarryLimit = 10;

        [SerializeReference]
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();
    }
}
