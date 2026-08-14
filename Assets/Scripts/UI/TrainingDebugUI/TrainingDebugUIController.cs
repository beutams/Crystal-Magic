using CrystalMagic.Core;
using CrystalMagic.UI;
using Unity.Entities;

public sealed class TrainingDebugUIController : UIControllerBase<TrainingDebugUI, TrainingDebugUIModel>
{
    public TrainingDebugUIController(TrainingDebugUI view, TrainingDebugUIModel model)
        : base(view, model)
    {
    }

    protected override void OnOpen()
    {
        View.BindModel(Model);
        Bindings.Bind(() => View.ToggleRequested += OnToggleRequested, () => View.ToggleRequested -= OnToggleRequested);
        Bindings.Bind(() => View.SpawnRequested += OnSpawnRequested, () => View.SpawnRequested -= OnSpawnRequested);
        Bindings.Bind(() => View.UnitControlRequested += OnUnitControlRequested, () => View.UnitControlRequested -= OnUnitControlRequested);
        Bindings.Bind(() => View.BackRequested += OnBackRequested, () => View.BackRequested -= OnBackRequested);
        Bindings.Bind(() => View.UnitSelected += OnUnitSelected, () => View.UnitSelected -= OnUnitSelected);
        Bindings.Bind(() => View.ClearAIRequested += OnClearAIRequested, () => View.ClearAIRequested -= OnClearAIRequested);
        Bindings.Bind(() => View.ClearStateTransitionsRequested += OnClearStateTransitionsRequested, () => View.ClearStateTransitionsRequested -= OnClearStateTransitionsRequested);
        Bindings.Bind(() => View.FacingRequested += OnFacingRequested, () => View.FacingRequested -= OnFacingRequested);
        Bindings.Bind(() => View.CastSkillRequested += OnCastSkillRequested, () => View.CastSkillRequested -= OnCastSkillRequested);
        Bindings.Bind(() => View.ForceStateRequested += OnForceStateRequested, () => View.ForceStateRequested -= OnForceStateRequested);
        Model.RefreshRuntime(true);
    }

    protected override void OnUpdate()
    {
        Model.RefreshRuntime();
    }

    private void OnToggleRequested() => Model.ToggleExpanded();
    private void OnUnitControlRequested() => Model.OpenUnitControl();
    private void OnBackRequested() => Model.CloseUnitControl();
    private void OnUnitSelected(EntitySelection selection) => Model.SelectUnit(selection);

    private void OnSpawnRequested()
    {
        TrainingDebugCommandQueue.Enqueue(new TrainingDebugCommand(
            TrainingDebugCommandType.SpawnUnit,
            Entity.Null,
            View.GetUnitName()));
    }

    private void OnClearAIRequested()
    {
        TrainingDebugCommandQueue.Enqueue(new TrainingDebugCommand(
            TrainingDebugCommandType.ClearAI,
            Model.SelectedEntity,
            null));
    }

    private void OnClearStateTransitionsRequested()
    {
        TrainingDebugCommandQueue.Enqueue(new TrainingDebugCommand(
            TrainingDebugCommandType.ClearStateTransitions,
            Model.SelectedEntity,
            null));
    }

    private void OnFacingRequested(string direction)
    {
        TrainingDebugCommandQueue.Enqueue(new TrainingDebugCommand(
            TrainingDebugCommandType.SetFacing,
            Model.SelectedEntity,
            direction));
    }

    private void OnCastSkillRequested()
    {
        TrainingDebugCommandQueue.Enqueue(new TrainingDebugCommand(
            TrainingDebugCommandType.CastSkill,
            Model.SelectedEntity,
            View.GetSkillId()));
    }

    private void OnForceStateRequested()
    {
        TrainingDebugCommandQueue.Enqueue(new TrainingDebugCommand(
            TrainingDebugCommandType.ForceState,
            Model.SelectedEntity,
            View.GetStateName()));
    }
}
