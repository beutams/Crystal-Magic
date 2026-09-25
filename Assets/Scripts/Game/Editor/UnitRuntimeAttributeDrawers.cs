using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Editor.Data;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using Unity.Transforms;

namespace CrystalMagic.Editor.Unit
{
    [FactoryKey("Transform", 0)]
    public sealed class UnitTransformRuntimeDrawer : IUnitRuntimeComponentDrawer
    {
        public Type ComponentType => typeof(LocalTransform);

        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<LocalTransform>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            LocalTransform transform = context.GetComponent<LocalTransform>();
            UnitEditorWindow.DrawSectionHeader("Transform");
            EditorGUILayout.Vector3Field("Position", transform.Position);
            EditorGUILayout.FloatField("Rotation", transform.Rotation.value.w);
            EditorGUILayout.FloatField("Scale", transform.Scale);
        }
    }

    [FactoryKey("Faction", 10)]
    public sealed class UnitFactionRuntimeDrawer : IUnitRuntimeComponentDrawer
    {
        public Type ComponentType => typeof(UnitFactionComponent);

        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitFactionComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitFactionComponent faction = context.GetComponent<UnitFactionComponent>();
            UnitEditorWindow.DrawSectionHeader("Faction");
            EditorGUILayout.TextField("Faction", faction.Value.ToString());
        }
    }

    [FactoryKey("Move", 20)]
    public sealed class UnitMoveRuntimeDrawer : IUnitRuntimeComponentDrawer
    {
        public Type ComponentType => typeof(UnitMoveComponent);

        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitMoveComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitMoveComponent move = context.GetComponent<UnitMoveComponent>();
            UnitEditorWindow.DrawSectionHeader("Move");
            EditorGUILayout.FloatField("Move Speed", UnitModifierResolver.GetMoveSpeed(context.EntityManager, context.Entity));
            EditorGUILayout.FloatField("Acceleration", UnitModifierResolver.GetMaxAcceleration(context.EntityManager, context.Entity));
            EditorGUILayout.Vector2Field("Direction", move.Direction);
            EditorGUILayout.FloatField("State Move Multiplier", move.StateMoveMultiplier);
            EditorGUILayout.Vector2Field("Velocity", move.Velocity);
        }
    }

    [FactoryKey("Vitality", 30)]
    public sealed class UnitVitalityRuntimeDrawer : IUnitRuntimeComponentDrawer
    {
        public Type ComponentType => typeof(UnitVitalityComponent);

        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitVitalityComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitVitalityComponent vitality = context.GetComponent<UnitVitalityComponent>();
            UnitEditorWindow.DrawSectionHeader("Vitality");
            EditorGUILayout.FloatField("Current Health", vitality.CurrentHealth);
            EditorGUILayout.FloatField("Max Health", UnitModifierResolver.GetMaxHealth(context.EntityManager, context.Entity));
            EditorGUILayout.FloatField("Health Regen", UnitModifierResolver.GetHealthRegen(context.EntityManager, context.Entity));
            EditorGUILayout.FloatField("Defense", UnitModifierResolver.GetDefense(context.EntityManager, context.Entity));
        }
    }

    [FactoryKey("Attack", 40)]
    public sealed class UnitAttackRuntimeDrawer : IUnitRuntimeComponentDrawer
    {
        public Type ComponentType => typeof(UnitAttackComponent);

        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitAttackComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitEditorWindow.DrawSectionHeader("Attack");
            EditorGUILayout.FloatField("Attack Power", UnitModifierResolver.GetAttackPower(context.EntityManager, context.Entity));
            EditorGUILayout.FloatField("Skill Range", UnitModifierResolver.GetSkillRange(context.EntityManager, context.Entity));
            EditorGUILayout.FloatField("Chant Speed (-100~100)", UnitModifierResolver.GetChantSpeedBonus(context.EntityManager, context.Entity));
        }
    }

    [FactoryKey("Element", 50)]
    public sealed class UnitElementRuntimeDrawer : IUnitRuntimeComponentDrawer
    {
        public Type ComponentType => typeof(UnitElementComponent);

        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitElementComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitEditorWindow.DrawSectionHeader("Element");
            EditorGUILayout.FloatField("Water", UnitModifierResolver.GetElementPower(context.EntityManager, context.Entity, CrystalMagic.Game.Data.Effects.ElementType.Water));
            EditorGUILayout.FloatField("Fire", UnitModifierResolver.GetElementPower(context.EntityManager, context.Entity, CrystalMagic.Game.Data.Effects.ElementType.Fire));
            EditorGUILayout.FloatField("Lightning", UnitModifierResolver.GetElementPower(context.EntityManager, context.Entity, CrystalMagic.Game.Data.Effects.ElementType.Lightning));
            EditorGUILayout.FloatField("Wind", UnitModifierResolver.GetElementPower(context.EntityManager, context.Entity, CrystalMagic.Game.Data.Effects.ElementType.Wind));
        }
    }

    [FactoryKey("Mana", 60)]
    public sealed class UnitManaRuntimeDrawer : IUnitRuntimeComponentDrawer
    {
        public Type ComponentType => typeof(UnitManaComponent);

        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitManaComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitManaComponent mana = context.GetComponent<UnitManaComponent>();
            UnitEditorWindow.DrawSectionHeader("Mana");
            EditorGUILayout.FloatField("Current Mana", mana.CurrentMana);
            EditorGUILayout.FloatField("Max Mana", UnitModifierResolver.GetMaxMp(context.EntityManager, context.Entity));
            EditorGUILayout.FloatField("Mana Regen", UnitModifierResolver.GetMpRegen(context.EntityManager, context.Entity));
        }
    }

    [FactoryKey("Perception", 70)]
    public sealed class UnitPerceptionRuntimeDrawer : IUnitRuntimeComponentDrawer
    {
        public Type ComponentType => typeof(UnitPerceptionComponent);

        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitPerceptionComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitEditorWindow.DrawSectionHeader("Perception");
            UnitPerceptionComponent perception = context.GetComponent<UnitPerceptionComponent>();
            EditorGUILayout.FloatField("Search Radius", perception.SearchRadius);
            if (context.EntityManager.HasBuffer<UnitPerceptionUnitElement>(context.Entity))
                EditorGUILayout.IntField("Nearby Unit Count", context.EntityManager.GetBuffer<UnitPerceptionUnitElement>(context.Entity).Length);
        }
    }

    [FactoryKey("Variables", 80)]
    public sealed class UnitVariableRuntimeDrawer : IUnitRuntimeComponentDrawer
    {
        public Type ComponentType => typeof(UnitVariableComponent);

        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitVariableComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitEditorWindow.DrawSectionHeader("Variables");
            UnitVariableComponent localVariables = context.EntityManager.GetComponentData<UnitVariableComponent>(context.Entity);
            Entity other = localVariables.Other;
            bool resolved = UnitVariableSource.TryGetBuffer(
                context.EntityManager,
                context.Entity,
                out DynamicBuffer<UnitVariableElement> variables);
            int count = resolved ? variables.Length : 0;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Other", other == Entity.Null ? "(none)" : other.ToString());
                EditorGUILayout.IntField("Count", count);
                if (!resolved)
                {
                    EditorGUILayout.TextField("Values", "(local buffer unavailable)");
                    return;
                }

                if (count == 0)
                {
                    EditorGUILayout.TextField("Values", "(none)");
                    return;
                }

                List<UnitVariableElement> entries = new(variables.Length);
                for (int index = 0; index < variables.Length; index++)
                    entries.Add(variables[index]);

                entries.Sort((left, right) => left.Key.CompareTo(right.Key));
                for (int i = 0; i < entries.Count; i++)
                {
                    UnitVariableElement entry = entries[i];
                    DrawValue(entry.Key.ToString(), entry.Value);
                }
            }
        }

        private static void DrawValue(string key, UnitSourceValue value)
        {
            string label = $"{key} ({value.Type})";
            switch (value.Type)
            {
                case UnitValueType.Bool:
                    EditorGUILayout.Toggle(label, value.Bool != 0);
                    break;
                case UnitValueType.Int:
                    EditorGUILayout.IntField(label, value.Int);
                    break;
                case UnitValueType.Float:
                    EditorGUILayout.FloatField(label, value.Float);
                    break;
                case UnitValueType.Float2:
                    EditorGUILayout.Vector2Field(label, new Vector2(value.Float2.x, value.Float2.y));
                    break;
                case UnitValueType.Float3:
                    EditorGUILayout.Vector3Field(label, new Vector3(value.Float3.x, value.Float3.y, value.Float3.z));
                    break;
                case UnitValueType.Entity:
                    EditorGUILayout.TextField(label, value.Entity.ToString());
                    break;
                case UnitValueType.String:
                    EditorGUILayout.TextField(label, value.String.ToString());
                    break;
                default:
                    EditorGUILayout.TextField(label, "(none)");
                    break;
            }
        }
    }

    [FactoryKey("Animation", 75)]
    public sealed class UnitAnimationRuntimeDrawer : IUnitRuntimeComponentDrawer
    {
        public Type ComponentType => typeof(UnitAnimationComponent);

        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitAnimationComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitAnimationComponent animation = context.GetComponent<UnitAnimationComponent>();
            UnitEditorWindow.DrawSectionHeader("Animation");
            EditorGUILayout.TextField("Requested", animation.AnimationName.ToString());
            EditorGUILayout.TextField("Playing", animation.PlayingAnimationName.ToString());
            EditorGUILayout.TextField("Facing", animation.LastTwoDirectionFacing.ToString());
            EditorGUILayout.FloatField("Elapsed", animation.ElapsedSeconds);
            EditorGUILayout.FloatField("Sample Time", animation.CurrentSampleTime);
            EditorGUILayout.FloatField("Clip Length", animation.CurrentClipLength);
        }
    }

    [FactoryKey("SkillRelease", 90)]
    public sealed class UnitSkillReleaseRuntimeDrawer : IUnitRuntimeComponentDrawer
    {
        public Type ComponentType => typeof(UnitSkillReleaseComponent);

        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitSkillReleaseComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitEditorWindow.DrawSectionHeader("Skill Release");
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.Toggle("Direct Dispatch", true);
        }
    }

}
