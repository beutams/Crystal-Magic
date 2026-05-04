using System.Collections.Generic;
using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    [FactoryKey("Faction", 0)]
    public sealed class UnitFactionAttributeDrawer : IUnitEditorAttributeDrawer
    {
        public bool CanDraw(UnitEditorDrawerContext context)
        {
            return context.HasAuthoring<UnitFactionAuthoring>();
        }

        public void Draw(UnitEditorDrawerContext context)
        {
            UnitFactionAuthoring factionAuthoring = context.GetAuthoring<UnitFactionAuthoring>();
            if (factionAuthoring == null)
                return;

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Faction");

            UnitFactionType newFaction = (UnitFactionType)EditorGUILayout.EnumPopup("Faction", factionAuthoring.Faction);
            if (newFaction != factionAuthoring.Faction)
            {
                factionAuthoring.Faction = newFaction;
                context.MarkPrefabDirty(factionAuthoring);
            }
        }
    }

    [FactoryKey("Move", 10)]
    public sealed class UnitMoveAttributeDrawer : IUnitEditorAttributeDrawer
    {
        public bool CanDraw(UnitEditorDrawerContext context)
        {
            return context.HasAuthoring<UnitMoveAuthoring>();
        }

        public void Draw(UnitEditorDrawerContext context)
        {
            UnitMoveModuleData module = context.GetOrCreateModule<UnitMoveModuleData>();
            if (module == null)
                return;

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Move");
            module.BaseMoveSpeed = EditorGUILayout.FloatField("Base Move Speed", module.BaseMoveSpeed);
            module.BaseMaxAcceleration = EditorGUILayout.FloatField("Base Max Acceleration", module.BaseMaxAcceleration);
        }
    }

    [FactoryKey("Vitality", 20)]
    public sealed class UnitVitalityAttributeDrawer : IUnitEditorAttributeDrawer
    {
        public bool CanDraw(UnitEditorDrawerContext context)
        {
            return context.HasAuthoring<UnitVitalityAuthoring>();
        }

        public void Draw(UnitEditorDrawerContext context)
        {
            UnitVitalityModuleData module = context.GetOrCreateModule<UnitVitalityModuleData>();
            if (module == null)
                return;

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Vitality");
            module.BaseMaxHealth = EditorGUILayout.FloatField("Base Max Health", module.BaseMaxHealth);
            module.BaseHealthRegenPerSecond = EditorGUILayout.FloatField("Base HP Regen / Sec", module.BaseHealthRegenPerSecond);
            module.BaseDefense = EditorGUILayout.FloatField("Base Defense", module.BaseDefense);
        }
    }

    [FactoryKey("Attack", 30)]
    public sealed class UnitAttackAttributeDrawer : IUnitEditorAttributeDrawer
    {
        public bool CanDraw(UnitEditorDrawerContext context)
        {
            return context.HasAuthoring<UnitAttackAuthoring>();
        }

        public void Draw(UnitEditorDrawerContext context)
        {
            UnitAttackModuleData module = context.GetOrCreateModule<UnitAttackModuleData>();
            if (module == null)
                return;

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Attack");
            module.BaseAttackPower = EditorGUILayout.FloatField("Base Attack Power", module.BaseAttackPower);
            module.BaseSkillRange = EditorGUILayout.FloatField("Base Skill Range", module.BaseSkillRange);
            module.BaseActionSpeedBonus = EditorGUILayout.FloatField("Action Speed Bonus", module.BaseActionSpeedBonus);
            module.BaseChantSpeedBonus = EditorGUILayout.FloatField("Chant Speed Bonus", module.BaseChantSpeedBonus);
            module.BaseWaterPowerBonus = EditorGUILayout.FloatField("Water Power Bonus", module.BaseWaterPowerBonus);
            module.BaseFirePowerBonus = EditorGUILayout.FloatField("Fire Power Bonus", module.BaseFirePowerBonus);
            module.BaseLightningPowerBonus = EditorGUILayout.FloatField("Lightning Power Bonus", module.BaseLightningPowerBonus);
            module.BaseWindPowerBonus = EditorGUILayout.FloatField("Wind Power Bonus", module.BaseWindPowerBonus);
        }
    }

    [FactoryKey("Mana", 40)]
    public sealed class UnitManaAttributeDrawer : IUnitEditorAttributeDrawer
    {
        public bool CanDraw(UnitEditorDrawerContext context)
        {
            return context.HasAuthoring<UnitManaAuthoring>();
        }

        public void Draw(UnitEditorDrawerContext context)
        {
            UnitManaModuleData module = context.GetOrCreateModule<UnitManaModuleData>();
            if (module == null)
                return;

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Mana");
            module.BaseMaxMp = EditorGUILayout.FloatField("Base Max MP", module.BaseMaxMp);
            module.BaseMpRegenPerSecond = EditorGUILayout.FloatField("Base MP Regen / Sec", module.BaseMpRegenPerSecond);
        }
    }

    [FactoryKey("Perception", 50)]
    public sealed class UnitPerceptionAttributeDrawer : IUnitEditorAttributeDrawer
    {
        public bool CanDraw(UnitEditorDrawerContext context)
        {
            return context.HasAuthoring<UnitPerceptionAuthoring>();
        }

        public void Draw(UnitEditorDrawerContext context)
        {
            UnitPerceptionAuthoring perceptionAuthoring = context.GetAuthoring<UnitPerceptionAuthoring>();
            if (perceptionAuthoring == null)
                return;

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Perception");

            float newSearchRadius = EditorGUILayout.FloatField("Search Radius", perceptionAuthoring.SearchRadius);
            if (!Mathf.Approximately(newSearchRadius, perceptionAuthoring.SearchRadius))
            {
                perceptionAuthoring.SearchRadius = newSearchRadius;
                context.MarkPrefabDirty(perceptionAuthoring);
            }
        }
    }

    [FactoryKey("NPCInteractable", 60)]
    public sealed class NPCInteractableAttributeDrawer : IUnitEditorAttributeDrawer
    {
        public bool CanDraw(UnitEditorDrawerContext context)
        {
            return context.HasAuthoring<NPCInteractableAuthoring>();
        }

        public void Draw(UnitEditorDrawerContext context)
        {
            NPCInteractableAuthoring npcAuthoring = context.GetAuthoring<NPCInteractableAuthoring>();
            if (npcAuthoring == null)
                return;

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("NPC Interaction");

            List<NPCData> npcRows = EditorComponents.Data.FindAll<NPCData>(_ => true)
                .OrderBy(row => row.Id)
                .ToList();

            List<string> options = new() { "Unbound" };
            int selectedIndex = 0;
            for (int i = 0; i < npcRows.Count; i++)
            {
                NPCData row = npcRows[i];
                options.Add($"[{row.Id}] {row.DisplayName} ({row.NPC})");
                if (row.Id == npcAuthoring.NpcDataId)
                    selectedIndex = i + 1;
            }

            int newIndex = EditorGUILayout.Popup("NPC Data", selectedIndex, options.ToArray());
            int newNpcId = newIndex == 0 ? 0 : npcRows[newIndex - 1].Id;
            float newRange = EditorGUILayout.FloatField("Interact Range", npcAuthoring.InteractRange);

            if (newNpcId != npcAuthoring.NpcDataId || !Mathf.Approximately(newRange, npcAuthoring.InteractRange))
            {
                npcAuthoring.NpcDataId = newNpcId;
                npcAuthoring.InteractRange = newRange;
                context.MarkPrefabDirty(npcAuthoring);
            }
        }
    }
}
