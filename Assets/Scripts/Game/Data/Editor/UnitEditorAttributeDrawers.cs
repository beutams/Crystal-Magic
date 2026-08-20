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

    [FactoryKey("Buff", 55)]
    public sealed class UnitBuffAttributeDrawer : IUnitEditorAttributeDrawer
    {
        private sealed class BuffOption
        {
            public int Id;
            public string Label;
        }

        public bool CanDraw(UnitEditorDrawerContext context)
        {
            return context.HasAuthoring<UnitBuffRuntimeAuthoring>();
        }

        public void Draw(UnitEditorDrawerContext context)
        {
            UnitBuffModuleData module = context.GetOrCreateModule<UnitBuffModuleData>();
            if (module == null)
                return;

            module.Buffs ??= new List<UnitInitialBuffEntry>();
            List<BuffOption> buffOptions = BuildBuffOptions();

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Buffs");

            for (int i = 0; i < module.Buffs.Count; i++)
            {
                UnitInitialBuffEntry entry = module.Buffs[i] ?? new UnitInitialBuffEntry();
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(BuildBuffHeader(i, entry), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Up", GUILayout.Width(40f)) && i > 0)
                {
                    (module.Buffs[i - 1], module.Buffs[i]) = (module.Buffs[i], module.Buffs[i - 1]);
                }
                if (GUILayout.Button("Down", GUILayout.Width(52f)) && i < module.Buffs.Count - 1)
                {
                    (module.Buffs[i + 1], module.Buffs[i]) = (module.Buffs[i], module.Buffs[i + 1]);
                }
                if (GUILayout.Button("Delete", GUILayout.Width(56f)))
                {
                    module.Buffs.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                entry.BuffId = DrawBuffPopup("Buff", entry.BuffId, buffOptions);
                entry.DurationSeconds = EditorGUILayout.FloatField("Duration Seconds", entry.DurationSeconds);
                entry.StackCount = Mathf.Max(1, EditorGUILayout.IntField("Stack Count", entry.StackCount));
                module.Buffs[i] = entry;
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Buff"))
                module.Buffs.Add(new UnitInitialBuffEntry());
        }

        private static List<BuffOption> BuildBuffOptions()
        {
            List<BuffOption> options = new()
            {
                new BuffOption { Id = -1, Label = "None" }
            };

            foreach (BuffData row in EditorComponents.Data.FindAll<BuffData>(_ => true).OrderBy(row => row.Id))
            {
                options.Add(new BuffOption
                {
                    Id = row.Id,
                    Label = $"[{row.Id}] {row.Name}",
                });
            }

            return options;
        }

        private static int DrawBuffPopup(string label, int currentId, List<BuffOption> options)
        {
            int selectedIndex = 0;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Id == currentId)
                {
                    selectedIndex = i;
                    break;
                }
            }

            string[] labels = options.Select(option => option.Label).ToArray();
            int newIndex = EditorGUILayout.Popup(label, selectedIndex, labels);
            return newIndex >= 0 && newIndex < options.Count ? options[newIndex].Id : currentId;
        }

        private static string BuildBuffHeader(int index, UnitInitialBuffEntry entry)
        {
            string label = $"Buff {index + 1}";
            if (entry == null || entry.BuffId < 0)
                return label;

            BuffData buff = EditorComponents.Data.Get<BuffData>(entry.BuffId);
            if (buff == null)
                return $"{label} | Buff {entry.BuffId}";

            return $"{label} | {buff.Name}";
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

    [FactoryKey("NPCInteractableComponent", 70)]
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
