using System;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.UI;
using Unity.Entities;
using UnityEngine;

public sealed class NPCCameraInteractionNodeRunner : NPCInteractionNodeRunner
{
    private readonly NPCCameraInteractionNodeData _node;
    private IDisposable _lease;
    private float _elapsed;
    public NPCCameraInteractionNodeRunner(NPCCameraInteractionNodeData node) => _node = node;
    public override void Enter(NPCInteractionSession session)
    {
        Entity target = NPCSequenceUtility.FindEntity(session, _node.Target);
        if (target == Entity.Null) { session.Cancel(); return; }
        _lease = CameraComponent.Instance.AcquireFollowTarget(session.World, target, Mathf.Max(.1f, _node.Smooth));
    }
    public override void Update(NPCInteractionSession session, float deltaTime) => _elapsed += deltaTime;
    public override bool IsCompleted(NPCInteractionSession session) => _elapsed >= Mathf.Max(.1f, _node.Duration);
    public override void Exit(NPCInteractionSession session) { _lease?.Dispose(); _lease = null; }
    public override void Cancel(NPCInteractionSession session) => Exit(session);
}

public sealed class NPCGuideInteractionNodeRunner : NPCInteractionNodeRunner
{
    private readonly NPCGuideInteractionNodeData _node;
    public NPCGuideInteractionNodeRunner(NPCGuideInteractionNodeData node) => _node = node;
    public override void Enter(NPCInteractionSession session)
    {
        session.ClearGuide();
        if (_node.Clear) return;
        GuideUI view = UIComponent.Instance.Open<GuideUI>(new GuideUIOpenData { Session = session, Node = _node });
        if (view == null) { session.Cancel(); return; }
        session.Guide = new PresentationLease(view, UIComponent.Instance.GetModel<GuideUIModel>(view));
    }
    public override bool IsCompleted(NPCInteractionSession session) => true;

    private sealed class PresentationLease : IDisposable
    {
        private GuideUI _view;
        private readonly GuideUIModel _model;
        public PresentationLease(GuideUI view, GuideUIModel model) { _view = view; _model = model; }
        public void Dispose()
        {
            if (_view != null && UIComponent.Instance.IsManaged(_view) &&
                ReferenceEquals(UIComponent.Instance.GetModel<GuideUIModel>(_view), _model))
                UIComponent.Instance.ReleaseUI(_view);
            _view = null;
        }
    }
}

public sealed class NPCWaitConditionInteractionNodeRunner : NPCInteractionNodeRunner
{
    private readonly NPCWaitConditionInteractionNodeData _node;
    public NPCWaitConditionInteractionNodeRunner(NPCWaitConditionInteractionNodeData node) => _node = node;
    public override void Enter(NPCInteractionSession session) { }
    public override bool IsCompleted(NPCInteractionSession session) => NPCSequenceUtility.Check(_node);
}

public sealed class NPCProgressInteractionNodeRunner : NPCInteractionNodeRunner
{
    private readonly NPCProgressInteractionNodeData _node;
    public NPCProgressInteractionNodeRunner(NPCProgressInteractionNodeData node) => _node = node;
    public override void Enter(NPCInteractionSession session)
    {
        if (!NPCSequenceUtility.SaveProgress(_node.Variable, _node.Value)) session.Cancel();
    }
    public override bool IsCompleted(NPCInteractionSession session) => true;
}

/// <summary>Managed story conditions. These read committed character data, never click events.</summary>
public static class NPCSequenceUtility
{
    public const string StageVariable = "intro_stage";
    public const int ReadyStage = 5;

    public static Entity FindEntity(NPCInteractionSession session, string target)
    {
        if (target == "actor") return session.Actor;
        if (session.World == null || !session.World.IsCreated) return Entity.Null;
        EntityManager manager = session.World.EntityManager;
        using EntityQuery query = manager.CreateEntityQuery(typeof(UnitInteractableComponent), typeof(Unity.Transforms.LocalToWorld));
        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (Entity entity in entities)
        {
            UnitInteractableComponent interactable = manager.GetComponentData<UnitInteractableComponent>(entity);
            if (interactable.Data.Kind == InteractionKind.Npc && interactable.IsEnabled != 0 &&
                DataComponent.Instance.Get<NPCData>(interactable.Data.DataId)?.NPC == target)
                return entity;
        }
        return Entity.Null;
    }

    public static bool Check(NPCWaitConditionInteractionNodeData node)
    {
        return Check(node, SaveDataComponent.Instance.GetCharacterData());
    }

    public static bool Check(NPCWaitConditionInteractionNodeData node, CharacterData character)
    {
        if (node == null) return false;
        switch (node.Condition)
        {
            case NPCWaitCondition.Always: return true;
            case NPCWaitCondition.Expression: return SaveDataComponent.Instance.Check(node.Value);
            case NPCWaitCondition.OwnItem: return Owns(character, node.ItemId);
            case NPCWaitCondition.MagicStone: return character?.Equipment?.MagicStoneId == node.ItemId;
            case NPCWaitCondition.PropSlot:
                return character?.Props?.Slots != null && node.Slot >= 0 && node.Slot < character.Props.Slots.Count &&
                    character.Props.Slots[node.Slot]?.ItemId == node.ItemId && character.Props.Slots[node.Slot].Quantity > 0;
            case NPCWaitCondition.SkillPrefix: return HasSkillPrefix(character, node.Items);
            case NPCWaitCondition.UIOpen: return IsUIOpen(node.Value);
            case NPCWaitCondition.UIClosed: return !IsUIOpen(node.Value);
            case NPCWaitCondition.All:
                if (node.Conditions == null || node.Conditions.Count == 0) return false;
                foreach (var condition in node.Conditions)
                    if (!Check(condition, character)) return false;
                return true;
            default: return false;
        }
    }

    public static bool IsUIOpen(string name) => name switch
    {
        "ShopUI" => UIComponent.Instance.FindOpen<ShopUI>() != null,
        "CharacterUI" => UIComponent.Instance.FindOpen<CharacterUI>() != null,
        "InteractionSelectUI" => UIComponent.Instance.FindOpen<InteractionSelectUI>() != null,
        _ => false,
    };

    public static bool Owns(CharacterData character, int itemId)
    {
        if (itemId < 0 || character == null) return false;
        if (character.Backpack?.Items != null)
            foreach (InventoryItemData item in character.Backpack.Items)
                if (item?.ItemId == itemId && item.Quantity > 0) return true;
        if (character.Equipment?.MagicStoneId == itemId) return true;
        if (character.Props?.Slots != null)
            foreach (CharacterPropSlotData slot in character.Props.Slots)
                if (slot?.ItemId == itemId && slot.Quantity > 0) return true;
        if (character.Skills?.Chains != null)
            foreach (SkillChainData chain in character.Skills.Chains)
                if (chain?.Slots != null)
                    foreach (SkillChainSlotData slot in chain.Slots)
                        if (slot?.SkillStoneItemId == itemId) return true;
        return false;
    }

    public static bool HasSkillPrefix(CharacterData character, System.Collections.Generic.IReadOnlyList<int> items)
    {
        var chains = character?.Skills?.Chains;
        var slots = chains != null && chains.Length > 0 ? chains[0]?.Slots : null;
        if (items == null || items.Count == 0 || slots == null || slots.Count < items.Count) return false;
        for (int i = 0; i < items.Count; i++)
            if (slots[i]?.SkillStoneItemId != items[i]) return false;
        return true;
    }

    public static bool SaveProgress(string variable, double value)
    {
        SaveDataComponent save = SaveDataComponent.Instance;
        if (string.IsNullOrWhiteSpace(variable) || !save.ContainsVariable(variable)) return false;
        double previous = save.GetVariable(variable);
        save.SetVariable(variable, value);
        if (save.Save()) return true;
        save.SetVariable(variable, previous);
        return false;
    }

    public static void CompleteIntroOnDungeonArrival()
    {
        SaveDataComponent save = SaveDataComponent.Instance;
        World world = GameWorldManager.GameWorld;
        if (!TownIntroTriggerUtility.IsPending(save) || world == null || !world.IsCreated ||
            TransitionComponent.Instance.IsTransitioning || save.GetVariable(StageVariable) != ReadyStage ||
            !GameWorldContextUtility.TryGet(world.EntityManager, out GameWorldContextComponent context) ||
            context.Role != GameWorldRole.Standalone || context.SceneMode != GameSceneMode.Dungeon ||
            !GameRuntimeStateUtility.TryGetPlayerEntity(out _, out _)) return;
        SaveProgress(TownIntroTriggerUtility.CompletionVariable, 1d);
    }

    public static bool AllowsInteraction(EntityManager manager, Entity target)
    {
        if (!GameWorldContextUtility.TryGet(manager, out GameWorldContextComponent context) ||
            context.Role != GameWorldRole.Standalone || context.SceneMode != GameSceneMode.Town ||
            !TownIntroTriggerUtility.IsPending(SaveDataComponent.Instance)) return true;
        string expected = (int)SaveDataComponent.Instance.GetVariable(StageVariable) switch
        {
            1 => "NPCEquipShop", 2 => "NPCPropShop", 3 => "NPCSkillShop", 5 => "NPCTraining", _ => "",
        };
        if (!manager.Exists(target) || !manager.HasComponent<UnitInteractableComponent>(target)) return false;
        var data = manager.GetComponentData<UnitInteractableComponent>(target).Data;
        return data.Kind == InteractionKind.Npc && DataComponent.Instance.Get<NPCData>(data.DataId)?.NPC == expected;
    }
}
