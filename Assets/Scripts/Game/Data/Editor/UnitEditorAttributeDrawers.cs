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
            {
                return;
            }

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Faction");

            UnitFactionType newFaction = (UnitFactionType)EditorGUILayout.EnumPopup("阵营", factionAuthoring.Faction);
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
            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Move（移动）");
            context.Unit.BaseMoveSpeed = EditorGUILayout.FloatField("最大速度", context.Unit.BaseMoveSpeed);
            context.Unit.BaseMaxAcceleration = EditorGUILayout.FloatField("最大加速度", context.Unit.BaseMaxAcceleration);
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
            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Vitality（生存）");
            context.Unit.BaseMaxHealth = EditorGUILayout.FloatField("最大生命值", context.Unit.BaseMaxHealth);
            context.Unit.BaseDefense = EditorGUILayout.FloatField("防御力", context.Unit.BaseDefense);
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
            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Attack（攻击）");
            context.Unit.BaseAttackPower = EditorGUILayout.FloatField("攻击力", context.Unit.BaseAttackPower);
            context.Unit.BaseSkillRange = EditorGUILayout.FloatField("技能范围", context.Unit.BaseSkillRange);
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
            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Mana（法力）");
            context.Unit.BaseMaxMp = EditorGUILayout.FloatField("最大魔力值", context.Unit.BaseMaxMp);
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
            {
                return;
            }

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Perception（感知）");

            float newSearchRadius = EditorGUILayout.FloatField("搜索范围", perceptionAuthoring.SearchRadius);
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
            {
                return;
            }

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("NPC 交互");

            List<NPCData> npcRows = EditorComponents.Data.FindAll<NPCData>(_ => true)
                .OrderBy(row => row.Id)
                .ToList();

            List<string> options = new() { "未绑定" };
            int selectedIndex = 0;
            for (int i = 0; i < npcRows.Count; i++)
            {
                NPCData row = npcRows[i];
                options.Add($"[{row.Id}] {row.DisplayName} ({row.NPC})");
                if (row.Id == npcAuthoring.NpcDataId)
                {
                    selectedIndex = i + 1;
                }
            }

            int newIndex = EditorGUILayout.Popup("NPCData", selectedIndex, options.ToArray());
            int newNpcId = newIndex == 0 ? 0 : npcRows[newIndex - 1].Id;
            float newRange = EditorGUILayout.FloatField("交互范围", npcAuthoring.InteractRange);

            if (newNpcId != npcAuthoring.NpcDataId || !Mathf.Approximately(newRange, npcAuthoring.InteractRange))
            {
                npcAuthoring.NpcDataId = newNpcId;
                npcAuthoring.InteractRange = newRange;
                context.MarkPrefabDirty(npcAuthoring);
            }
        }
    }
}
