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
            EditorGUILayout.FloatField("Move Speed", UnitModifierResolver.GetMoveSpeed(context.EntityManager, context.Entity));
            EditorGUILayout.FloatField("Acceleration", UnitModifierResolver.GetMaxAcceleration(context.EntityManager, context.Entity));
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
            EditorGUILayout.FloatField("Max Health", UnitModifierResolver.GetMaxHealth(context.EntityManager, context.Entity));
            EditorGUILayout.FloatField("Health Regen", UnitModifierResolver.GetHealthRegen(context.EntityManager, context.Entity));
            EditorGUILayout.FloatField("Defense", UnitModifierResolver.GetDefense(context.EntityManager, context.Entity));
        }
    }

    [FactoryKey("Attack", 40)]
    public sealed class UnitAttackRuntimeDrawer : IUnitRuntimeAttributeDrawer
    {
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
    public sealed class UnitElementRuntimeDrawer : IUnitRuntimeAttributeDrawer
    {
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
    public sealed class UnitManaRuntimeDrawer : IUnitRuntimeAttributeDrawer
    {
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
    public sealed class UnitPerceptionRuntimeDrawer : IUnitRuntimeAttributeDrawer
    {
        public bool CanDraw(UnitRuntimeDrawerContext context) => context.HasComponent<UnitPerceptionComponent>();

        public void Draw(UnitRuntimeDrawerContext context)
        {
            UnitEditorWindow.DrawSectionHeader("Perception");
            UnitPerceptionComponent perception = context.GetComponent<UnitPerceptionComponent>();
            EditorGUILayout.FloatField("Search Radius", perception.SearchRadius);
            if (context.EntityManager.HasBuffer<UnitPerceptionEntityElement>(context.Entity))
                EditorGUILayout.IntField("Nearby Unit Count", context.EntityManager.GetBuffer<UnitPerceptionEntityElement>(context.Entity).Length);
        }
    }

}
