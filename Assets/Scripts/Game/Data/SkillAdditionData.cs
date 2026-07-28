using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data.Effects;
using Newtonsoft.Json;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    [System.Serializable]
    [ReadOnlyData]
    public class SkillAdditionData : DataRow
    {
        public string NameKey;
        public string DescriptionKey;
        public string IconPath;
        public List<SkillModifierEntry> Modifiers = new();
        public List<SkillFollowupEffectData> FollowupEffects = new();
        public List<SkillCastTaskData> CastTasks = new();
        [SerializeReference]
        public EffectData[] EffectChain = System.Array.Empty<EffectData>();

        [JsonIgnore]
        public string Name => LocalizationComponent.Resolve(NameKey);

        [JsonIgnore]
        public string Description => LocalizationComponent.Resolve(DescriptionKey);
    }
}
