using CrystalMagic.Core;
using CrystalMagic.Editor.Data;
using UnityEditor;
using Unity.Transforms;

namespace CrystalMagic.Editor.Unit
{
    [FactoryKey("Transform", 0)]
    public sealed class UnitTransformRuntimeDrawer : IUnitRuntimeAttributeDrawer
    {
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
    public sealed class UnitFactionRuntimeDrawer : IUnitRuntimeAttributeDrawer
    {
        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitFactionComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitFactionComponent faction = context.GetComponent<UnitFactionComponent>();
            UnitEditorWindow.DrawSectionHeader("Faction");
            EditorGUILayout.TextField("Faction", faction.Value.ToString());
        }
    }

    [FactoryKey("Move", 20)]
    public sealed class UnitMoveRuntimeDrawer : IUnitRuntimeAttributeDrawer
    {
        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitMoveComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitMoveComponent move = context.GetComponent<UnitMoveComponent>();
            UnitEditorWindow.DrawSectionHeader("Move");
            EditorGUILayout.FloatField("Move Speed", move.RealMoveSpeed);
            EditorGUILayout.FloatField("Acceleration", move.RealMaxAcceleration);
            EditorGUILayout.Vector2Field("Direction", move.Direction);
            EditorGUILayout.FloatField("State Move Multiplier", move.StateMoveMultiplier);
            EditorGUILayout.Vector2Field("Velocity", move.Velocity);
        }
    }

    [FactoryKey("Vitality", 30)]
    public sealed class UnitVitalityRuntimeDrawer : IUnitRuntimeAttributeDrawer
    {
        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitVitalityComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitVitalityComponent vitality = context.GetComponent<UnitVitalityComponent>();
            UnitEditorWindow.DrawSectionHeader("Vitality");
            EditorGUILayout.FloatField("Current Health", vitality.CurrentHealth);
            EditorGUILayout.FloatField("Max Health", vitality.RealMaxHealth);
            EditorGUILayout.FloatField("Health Regen", vitality.RealHealthRegenPerSecond);
            EditorGUILayout.FloatField("Defense", vitality.RealDefense);
        }
    }

    [FactoryKey("Attack", 40)]
    public sealed class UnitAttackRuntimeDrawer : IUnitRuntimeAttributeDrawer
    {
        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitAttackComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitAttackComponent attack = context.GetComponent<UnitAttackComponent>();
            UnitEditorWindow.DrawSectionHeader("Attack");
            EditorGUILayout.FloatField("Attack Power", attack.RealAttackPower);
            EditorGUILayout.FloatField("Skill Range", attack.RealSkillRange);
            EditorGUILayout.FloatField("Action Speed (-100~100)", attack.RealActionSpeedBonus);
            EditorGUILayout.FloatField("Chant Speed (-100~100)", attack.RealChantSpeedBonus);
        }
    }

    [FactoryKey("Element", 50)]
    public sealed class UnitElementRuntimeDrawer : IUnitRuntimeAttributeDrawer
    {
        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitElementComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitElementComponent element = context.GetComponent<UnitElementComponent>();
            UnitEditorWindow.DrawSectionHeader("Element");
            EditorGUILayout.FloatField("Water", element.WaterPower);
            EditorGUILayout.FloatField("Fire", element.FirePower);
            EditorGUILayout.FloatField("Lightning", element.LightningPower);
            EditorGUILayout.FloatField("Wind", element.WindPower);
        }
    }

    [FactoryKey("Mana", 60)]
    public sealed class UnitManaRuntimeDrawer : IUnitRuntimeAttributeDrawer
    {
        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitManaComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitManaComponent mana = context.GetComponent<UnitManaComponent>();
            UnitEditorWindow.DrawSectionHeader("Mana");
            EditorGUILayout.FloatField("Current Mana", mana.CurrentMana);
            EditorGUILayout.FloatField("Max Mana", mana.RealMaxMp);
            EditorGUILayout.FloatField("Mana Regen", mana.RealMpRegenPerSecond);
        }
    }

    [FactoryKey("Perception", 70)]
    public sealed class UnitPerceptionRuntimeDrawer : IUnitRuntimeAttributeDrawer
    {
        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitPerceptionComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitPerceptionComponent perception = context.GetComponent<UnitPerceptionComponent>();
            UnitEditorWindow.DrawSectionHeader("Perception");
            EditorGUILayout.FloatField("Search Radius", perception.SearchRadius);
            EditorGUILayout.Toggle("Has Target", perception.HasTarget);
            EditorGUILayout.Vector2Field("Target Position", perception.TargetPosition);
            EditorGUILayout.FloatField("Target Distance", perception.TargetDistance);
        }
    }

}
