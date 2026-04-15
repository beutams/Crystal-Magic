using System;
using System.Collections.Generic;
using CrystalMagic.Core;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    public class NPCData : DataRow
    {
        public string NPC;

        public string DisplayName;

        public List<NPCInteractionData> Interactions = new();

        public IEnumerable<NPCInteractionData> GetEnabledInteractions(SaveVariableData variables)
        {
            for (int i = 0; i < Interactions.Count; i++)
            {
                NPCInteractionData interaction = Interactions[i];
                if (interaction != null && interaction.IsEnabled(variables))
                {
                    yield return interaction;
                }
            }
        }
    }

    [Serializable]
    public class NPCInteractionData
    {
        public string Key;

        public string DisplayName;

        public string EnableExpression;

        public string ContentKey;

        public bool IsEnabled(SaveVariableData variables)
        {
            if (string.IsNullOrWhiteSpace(EnableExpression))
            {
                return true;
            }

            return variables != null && variables.Check(EnableExpression);
        }
    }
}
