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
            UnitFactionModuleData module = context.GetOrCreateModule<UnitFactionModuleData>();
            if (module == null)
                return;

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Faction");

            module.Faction = (UnitFactionType)EditorGUILayout.EnumPopup("Faction", module.Faction);
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
            module.BaseChantSpeedBonus = EditorGUILayout.FloatField("Chant Speed (-100~100)", module.BaseChantSpeedBonus);
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
            UnitPerceptionModuleData module = context.GetOrCreateModule<UnitPerceptionModuleData>();
            if (module == null)
                return;

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Perception");

            module.SearchRadius = Mathf.Max(0f, EditorGUILayout.FloatField("Search Radius", module.SearchRadius));
        }
    }

    [FactoryKey("Drop", 60)]
    public sealed class UnitDropAttributeDrawer : IUnitEditorAttributeDrawer
    {
        public bool CanDraw(UnitEditorDrawerContext context)
        {
            return context.HasAuthoring<UnitDropAuthoring>();
        }

        public void Draw(UnitEditorDrawerContext context)
        {
            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Drop");

            UnitDropModuleData module = context.GetOrCreateModule<UnitDropModuleData>();
            if (module == null)
                return;

            context.DrawInlineDropDataEditor(module);
        }
    }

    [FactoryKey("NPCInteraction", 70)]
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

            NPCData npcData = NPCAuthoringUtility.ResolveNpcData(npcAuthoring);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Prefab", context.AssetPath ?? string.Empty);
                EditorGUILayout.TextField("NPC Data", npcData != null
                    ? $"[{npcData.Id}] {npcData.DisplayName} ({npcData.NPC})"
                    : "Unbound");
            }
            float newRange = EditorGUILayout.FloatField("Interact Range", npcAuthoring.InteractRange);

            if (npcData == null)
            {
                EditorGUILayout.HelpBox("No NPCData row matches this prefab yet. Refresh prefabs in NPC Editor and save the NPC table first.", MessageType.Warning);
            }

            if (!Mathf.Approximately(newRange, npcAuthoring.InteractRange))
            {
                npcAuthoring.InteractRange = newRange;
                context.MarkPrefabDirty(npcAuthoring);
            }
        }
    }
    [FactoryKey("Dungeon Footprint", 60)]
    public sealed class UnitDungeonFootprintAttributeDrawer : IUnitEditorAttributeDrawer
    {
        public bool CanDraw(UnitEditorDrawerContext context)
        {
            return context.HasAuthoring<UnitDungeonFootprintAuthoring>();
        }

        public void Draw(UnitEditorDrawerContext context)
        {
            UnitDungeonFootprintModuleData module = context.GetOrCreateModule<UnitDungeonFootprintModuleData>();
            if (module == null)
                return;

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Dungeon Footprint");
            module.Width = Mathf.Max(1, EditorGUILayout.IntField("Width", module.Width));
            module.Height = Mathf.Max(1, EditorGUILayout.IntField("Height", module.Height));
        }
    }
}
