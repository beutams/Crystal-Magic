using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    [FactoryKey("Avoidance", 16)]
    public sealed class UnitAvoidanceAttributeDrawer : IUnitEditorAttributeDrawer
    {
        public bool CanDraw(UnitEditorDrawerContext context)
        {
            return context.HasAuthoring<UnitAvoidanceAuthoring>();
        }

        public void Draw(UnitEditorDrawerContext context)
        {
            UnitAvoidanceModuleData module = context.GetOrCreateModule<UnitAvoidanceModuleData>();
            if (module == null)
                return;

            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("Avoidance");
            module.NeighborDistance = Mathf.Max(0f, EditorGUILayout.FloatField("Neighbor Distance", module.NeighborDistance));
            module.MaxNeighbors = Mathf.Max(0, EditorGUILayout.IntField("Max Neighbors", module.MaxNeighbors));
            module.TimeHorizon = Mathf.Max(0.01f, EditorGUILayout.FloatField("Time Horizon", module.TimeHorizon));
            module.RadiusPadding = Mathf.Max(0f, EditorGUILayout.FloatField("Radius Padding", module.RadiusPadding));
        }
    }
}
