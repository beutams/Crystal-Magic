using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Transforms;

/// <summary>A renderless NPC using the ordinary NPC interaction state script.</summary>
public static class TownIntroTriggerUtility
{
    public const string CompletionVariable = "intro_completed";
    public const string NpcName = "TownIntroTrigger";
    public const int UnitDataId = 37;

    public static bool IsPending(SaveDataComponent save) =>
        save != null && save.ContainsVariable(CompletionVariable) && save.GetVariable(CompletionVariable) < 1d;

    public static Entity TryCreate(EntityManager manager, Entity player)
    {
        if (!IsPending(SaveDataComponent.Instance) || !manager.Exists(player) ||
            !manager.HasComponent<LocalTransform>(player) ||
            manager.HasComponent<UnitInitializationPendingTag>(player) ||
            !GameWorldContextUtility.TryGet(manager, out GameWorldContextComponent context) ||
            context.Role != GameWorldRole.Standalone || context.SceneMode != GameSceneMode.Town)
            return Entity.Null;

        NPCData npc = null;
        var table = DataComponent.Instance.GetTable<NPCData>();
        if (table != null)
            foreach (NPCData row in table.GetAll())
                if (row.NPC == NpcName) { npc = row; break; }
        if (npc == null)
            return Entity.Null;

        Entity trigger = manager.CreateEntity(typeof(LocalTransform), typeof(UnitInteractableComponent),
            typeof(UnitStateScriptComponent), typeof(UnitInitializationPendingTag), typeof(UnitVariableComponent));
        manager.SetName(trigger, NpcName);
        manager.SetComponentData(trigger, manager.GetComponentData<LocalTransform>(player));
        manager.SetComponentData(trigger, new UnitInteractableComponent
        {
            Data = new UnitInteractionData { Kind = InteractionKind.Npc, DataId = npc.Id },
            RangeSq = 0f, // Arrival is an event, not a proximity prompt; cannot walk out of it during initialization.
            IsEnabled = 1,
            HidePrompt = 1,
        });
        manager.SetComponentData(trigger, new UnitStateScriptComponent { UnitDataId = UnitDataId, DefinitionIndex = -1 });
        manager.AddBuffer<StateScriptGraphStateElement>(trigger);
        manager.AddBuffer<StateScriptNodeStateElement>(trigger);
        manager.AddBuffer<StateScriptSourceCommandElement>(trigger);
        manager.AddBuffer<StateScriptSourceCommandArgumentElement>(trigger);
        manager.AddBuffer<StateScriptExternalResultElement>(trigger);
        manager.AddBuffer<UnitVariableElement>(trigger);
        manager.AddBuffer<UnitVariableConsumerElement>(trigger);
        return trigger;
    }
}
