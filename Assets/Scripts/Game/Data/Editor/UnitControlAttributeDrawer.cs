using CrystalMagic.Core;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    [FactoryKey("Control", 55)]
    public sealed class UnitControlAttributeDrawer : IUnitEditorAttributeDrawer
    {
        public bool CanDraw(UnitEditorDrawerContext context) => context.HasAuthoring<UnitControlAuthoring>();

        public void Draw(UnitEditorDrawerContext context)
        {
            UnitControlAuthoring authoring = context.GetAuthoring<UnitControlAuthoring>();
            GUILayout.Space(8f);
            UnitEditorWindow.DrawSectionHeader("玩家硬控保护");
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle("启用同类硬控免疫", authoring.HardControlImmunityEnabled);
            float multiplier = Mathf.Max(0f, EditorGUILayout.FloatField("控制时长倍数", authoring.HardControlImmunityDurationMultiplier));
            float minimum = Mathf.Max(0f, EditorGUILayout.FloatField("最短间隔（秒）", authoring.HardControlImmunityMinimumSeconds));
            EditorGUILayout.HelpBox("仅玩家生效。结束后的同类免疫间隔 = max(最短间隔, 本次控制时长 × 倍数)。", MessageType.Info);
            if (!EditorGUI.EndChangeCheck())
                return;
            Undo.RecordObject(authoring, "Change player control immunity");
            authoring.HardControlImmunityEnabled = enabled;
            authoring.HardControlImmunityDurationMultiplier = multiplier;
            authoring.HardControlImmunityMinimumSeconds = minimum;
            context.MarkPrefabDirty(authoring);
        }
    }
}
