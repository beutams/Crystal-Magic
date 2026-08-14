// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Right-click Prefab -> Assets/Tools/Generate UIData to regenerate

using CrystalMagic.Core;
using UnityEngine;

public class TrainingDebugUIData : UIData
{
    public UINode ToggleButton;
    public UINode DebugPanel;
    public UINode CommandPage;
    public UINode UnitNameInput;
    public UINode SpawnButton;
    public UINode UnitControlButton;
    public UINode UnitControlPage;
    public UINode BackButton;
    public UINode UnitListContent;
    public UINode UnitListContent_UnitItem;
    public UINode InspectorText;
    public UINode ClearAIButton;
    public UINode ClearTransitionsButton;
    public UINode FaceLeftButton;
    public UINode FaceRightButton;
    public UINode FaceUpButton;
    public UINode FaceDownButton;
    public UINode SkillIdInput;
    public UINode CastSkillButton;
    public UINode StateNameInput;
    public UINode ForceStateButton;
    public UINode ResultText;

    public override void Bind(Transform root)
    {
        ToggleButton = UINode.From(Find(root, "ToggleButton"));
        DebugPanel = UINode.From(Find(root, "DebugPanel"));
        CommandPage = UINode.From(Find(root, "DebugPanel/CommandPage"));
        UnitNameInput = UINode.From(Find(root, "DebugPanel/CommandPage/CommandScrollView/Viewport/Content/UnitNameInput"));
        SpawnButton = UINode.From(Find(root, "DebugPanel/CommandPage/CommandScrollView/Viewport/Content/SpawnButton"));
        UnitControlButton = UINode.From(Find(root, "DebugPanel/CommandPage/CommandScrollView/Viewport/Content/UnitControlButton"));
        UnitControlPage = UINode.From(Find(root, "DebugPanel/UnitControlPage"));
        BackButton = UINode.From(Find(root, "DebugPanel/UnitControlPage/BackButton"));
        UnitListContent = UINode.From(Find(root, "DebugPanel/UnitControlPage/UnitListScrollView/Viewport/Content"));
        UnitListContent_UnitItem = UINode.From(Find(root, "DebugPanel/UnitControlPage/UnitListScrollView/Viewport/Content/UnitItem"));
        InspectorText = UINode.From(Find(root, "DebugPanel/UnitControlPage/InspectorText"));
        ClearAIButton = UINode.From(Find(root, "DebugPanel/UnitControlPage/ClearAIButton"));
        ClearTransitionsButton = UINode.From(Find(root, "DebugPanel/UnitControlPage/ClearTransitionsButton"));
        FaceLeftButton = UINode.From(Find(root, "DebugPanel/UnitControlPage/FaceLeftButton"));
        FaceRightButton = UINode.From(Find(root, "DebugPanel/UnitControlPage/FaceRightButton"));
        FaceUpButton = UINode.From(Find(root, "DebugPanel/UnitControlPage/FaceUpButton"));
        FaceDownButton = UINode.From(Find(root, "DebugPanel/UnitControlPage/FaceDownButton"));
        SkillIdInput = UINode.From(Find(root, "DebugPanel/UnitControlPage/SkillIdInput"));
        CastSkillButton = UINode.From(Find(root, "DebugPanel/UnitControlPage/CastSkillButton"));
        StateNameInput = UINode.From(Find(root, "DebugPanel/UnitControlPage/StateNameInput"));
        ForceStateButton = UINode.From(Find(root, "DebugPanel/UnitControlPage/ForceStateButton"));
        ResultText = UINode.From(Find(root, "DebugPanel/ResultText"));
    }
}
