using CrystalMagic.Game.Data;
using UnityEditor;
using UnityEngine;

internal static class NPCSequenceNodeDrawer
{
    public static bool Draw(NPCInteractionNodeData node)
    {
        switch (node)
        {
            case NPCCameraInteractionNodeData camera:
                camera.Target = EditorGUILayout.TextField("Target (actor / NPC name)", camera.Target);
                camera.Duration = Mathf.Max(.1f, EditorGUILayout.FloatField("Duration", camera.Duration));
                camera.Smooth = Mathf.Max(.1f, EditorGUILayout.FloatField("Smooth", camera.Smooth));
                return true;
            case NPCGuideInteractionNodeData guide:
                guide.Clear = EditorGUILayout.Toggle("Clear Guide", guide.Clear);
                guide.Target = EditorGUILayout.TextField("Target", guide.Target);
                guide.SecondaryTarget = EditorGUILayout.TextField("Drag Destination", guide.SecondaryTarget);
                guide.ContentKey = EditorGUILayout.TextField("Content Key", guide.ContentKey);
                guide.FallbackTarget = EditorGUILayout.TextField("Fallback NPC", guide.FallbackTarget);
                guide.FallbackContentKey = EditorGUILayout.TextField("Fallback Content Key", guide.FallbackContentKey);
                EditorGUILayout.HelpBox("Guide stays visible through WaitCondition. UI targets rebind after layout/reopen. Missing UI never blocks the whole screen.", MessageType.None);
                return true;
            case NPCWaitConditionInteractionNodeData wait:
                DrawCondition(wait);
                return true;
            case NPCProgressInteractionNodeData progress:
                progress.Variable = EditorGUILayout.TextField("Save Variable", progress.Variable);
                progress.Value = EditorGUILayout.DoubleField("Value", progress.Value);
                return true;
            default: return false;
        }
    }

    public static void DrawBranchCondition(NPCInteractionBranchData branch)
    {
        bool use = EditorGUILayout.Toggle("Runtime Condition", branch.RuntimeCondition != null);
        if (!use) { branch.RuntimeCondition = null; return; }
        branch.RuntimeCondition ??= new NPCWaitConditionInteractionNodeData();
        DrawCondition(branch.RuntimeCondition);
    }

    private static void DrawCondition(NPCWaitConditionInteractionNodeData wait)
    {
        wait.Condition = (NPCWaitCondition)EditorGUILayout.EnumPopup("Condition", wait.Condition);
        if (wait.Condition == NPCWaitCondition.All)
        {
            int count = Mathf.Clamp(EditorGUILayout.IntField("Conditions", wait.Conditions.Count), 0, 32);
            while (wait.Conditions.Count < count) wait.Conditions.Add(new NPCWaitConditionInteractionNodeData());
            while (wait.Conditions.Count > count) wait.Conditions.RemoveAt(wait.Conditions.Count - 1);
            EditorGUI.indentLevel++;
            foreach (var condition in wait.Conditions) DrawCondition(condition);
            EditorGUI.indentLevel--;
            return;
        }
        wait.Value = EditorGUILayout.TextField("Expression / UI Name", wait.Value);
        wait.ItemId = EditorGUILayout.IntField("Item Id", wait.ItemId);
        wait.Slot = EditorGUILayout.IntField("Slot (zero-based)", wait.Slot);
        wait.AllowInventory = EditorGUILayout.Toggle("Allow Inventory Key", wait.AllowInventory);
        int length = Mathf.Clamp(EditorGUILayout.IntField("Skill Prefix Count", wait.Items.Count), 0, 10);
        while (wait.Items.Count < length) wait.Items.Add(-1);
        while (wait.Items.Count > length) wait.Items.RemoveAt(wait.Items.Count - 1);
        for (int i = 0; i < length; i++) wait.Items[i] = EditorGUILayout.IntField("Skill Stone " + i, wait.Items[i]);
    }
}
