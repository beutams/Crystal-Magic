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
        public List<SkillAdditionCallbackData> Callbacks = new();

        [JsonIgnore]
        public string Name => LocalizationComponent.Resolve(NameKey);

        [JsonIgnore]
        public string Description => LocalizationComponent.Resolve(DescriptionKey);
    }

    [System.Serializable]
    public sealed class SkillAdditionCallbackData
    {
        public string EventName = string.Empty;
        public List<ConditionConfig> Conditions = new();

        [SerializeReference]
        public List<SkillAdditionActionData> Actions = new();
    }
}
