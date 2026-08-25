using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.UI;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(InteractionCandidateSystem))]
public partial class GameInteractionSystem : SystemBase
{
    private const string NpcSessionInputLockReason = "GameInteraction.NpcSession";

    private NPCInteractionNodeRunnerFactory _runnerFactory;
    private NPCInteractionSession _npcSession;
    private bool _npcInputLocked;

    protected override void OnCreate()
    {
        _runnerFactory = new NPCInteractionNodeRunnerFactory();
        NPCInteractionNodeRunnerRegistry.RegisterAll(_runnerFactory);
        Entity requestEntity = EntityManager.CreateEntity();
        EntityManager.AddComponentData(requestEntity, default(GameInteractionRequest));
    }

    protected override void OnDestroy()
    {
        _npcSession?.Cancel();
        _npcSession = null;
        SetInteractionActive(false);
        ReleaseNpcInput();
    }

    protected override void OnUpdate()
    {
        if (_npcSession != null && _npcSession.IsActive)
        {
            ClearPendingRequest();
            UpdateNpcSession(SystemAPI.Time.DeltaTime);
            return;
        }

        ConsumePendingRequest();
    }

    private void ConsumePendingRequest()
    {
        RefRW<GameInteractionRequest> requestRef = SystemAPI.GetSingletonRW<GameInteractionRequest>();
        if (requestRef.ValueRO.HasRequest == 0)
            return;

        GameInteractionRequest request = requestRef.ValueRO;
        requestRef.ValueRW = default;
        if (IsInteractionActive() || !TryValidateRequest(request, out UnitInteractableComponent interactable))
            return;

        switch (request.Data.Kind)
        {
            case InteractionKind.Drop:
                TryExecuteDrop(request);
                break;
            case InteractionKind.Treasure:
                TryOpenTreasure(request, interactable);
                break;
            case InteractionKind.Npc:
                TryStartNpcSession(request);
                break;
        }
    }

    private bool TryValidateRequest(in GameInteractionRequest request, out UnitInteractableComponent interactable)
    {
        interactable = default;
        if (request.Target == Entity.Null || !EntityManager.Exists(request.Target) ||
            !EntityManager.HasComponent<UnitInteractableComponent>(request.Target))
        {
            return false;
        }

        interactable = EntityManager.GetComponentData<UnitInteractableComponent>(request.Target);
        if (!GameInteractionTargetUtility.IsAvailable(EntityManager, request.Target, interactable) ||
            !GameInteractionTargetUtility.IsSameData(request.Data, interactable.Data))
        {
            return false;
        }

        if (request.Actor == Entity.Null || !EntityManager.Exists(request.Actor) ||
            !EntityManager.HasComponent<LocalTransform>(request.Actor) ||
            !EntityManager.HasComponent<LocalTransform>(request.Target))
        {
            return true;
        }

        float rangeSq = interactable.RangeSq;
        if (rangeSq <= 0f)
            return true;

        float3 actorPosition = EntityManager.GetComponentData<LocalTransform>(request.Actor).Position;
        float3 targetPosition = EntityManager.GetComponentData<LocalTransform>(request.Target).Position;
        return math.lengthsq((actorPosition - targetPosition).xy) <= rangeSq;
    }

    private void TryExecuteDrop(in GameInteractionRequest request)
    {
        int amount = math.max(0, request.Data.Amount);
        DropRewardType dropType = (DropRewardType)request.Data.Variant;
        if (amount <= 0)
            return;

        if (dropType == DropRewardType.Money)
        {
            SaveLocationData location = SaveDataComponent.Instance.GetLocationData();
            if (location?.AreaType == SaveAreaType.Dungeon && SaveDataComponent.Instance.GetDungeonRunData() is { } dungeonRun)
            {
                dungeonRun.RunMoney += amount;
                SaveDataComponent.Instance.NotifySaveDataChanged();
            }
            else if (SaveDataComponent.Instance.GetTownData() is { } townData)
            {
                townData.StashMoney += amount;
                SaveDataComponent.Instance.NotifyTownDataChanged();
            }

            MarkDestroyed(request.Target);
            return;
        }

        if (dropType != DropRewardType.Item || request.Data.DataId < 0)
            return;

        BackpackData backpack = SaveDataComponent.Instance.GetBackpackData();
        CharacterPropData props = SaveDataComponent.Instance.GetCharacterPropData();
        if (!InventoryUtility.CanAddItemToCharacterInventory(backpack, props, request.Data.DataId, amount))
        {
            ShowInventoryFullTip();
            return;
        }

        if (InventoryUtility.AddItemToCharacterInventory(backpack, props, request.Data.DataId, amount) != amount)
        {
            ShowInventoryFullTip();
            return;
        }

        SaveDataComponent.Instance.NotifyBackpackDataChanged();
        MarkDestroyed(request.Target);
    }

    private void TryOpenTreasure(in GameInteractionRequest request, UnitInteractableComponent interactable)
    {
        if (!EntityManager.HasComponent<TreasureComponent>(request.Target))
            return;

        TreasureComponent treasure = EntityManager.GetComponentData<TreasureComponent>(request.Target);
        if (treasure.IsOpened != 0)
            return;

        treasure.IsOpened = 1;
        EntityManager.SetComponentData(request.Target, treasure);
        interactable.IsEnabled = 0;
        EntityManager.SetComponentData(request.Target, interactable);
    }

    private void TryStartNpcSession(in GameInteractionRequest request)
    {
        NPCData npcData = DataComponent.Instance.Get<NPCData>(request.Data.DataId);
        if (npcData == null)
        {
            Debug.LogWarning($"[GameInteraction] NPCData not found for Id '{request.Data.DataId}'.");
            return;
        }

        NPCInteractionData interaction = SelectNpcInteraction(npcData);
        if (interaction == null || interaction.GetEntryNode() == null)
            return;

        _npcSession = new NPCInteractionSession(request.Target, npcData, interaction);
        SetInteractionActive(true);
        AcquireNpcInput();
        EventComponent.Instance.Publish(new NPCInteractionStartedEvent(request.Target, npcData, interaction));
        AdvanceNpcSessionUntilBlocked(0f);
    }

    private static NPCInteractionData SelectNpcInteraction(NPCData npcData)
    {
        NPCInteractionData selected = null;
        foreach (NPCInteractionData interaction in npcData.GetEnabledInteractions())
        {
            selected ??= interaction;
        }

        return selected;
    }

    private void UpdateNpcSession(float deltaTime)
    {
        if (_npcSession == null || !_npcSession.IsActive)
            return;

        if (!_npcSession.IsTargetValid(EntityManager))
        {
            FinishNpcSession(wasCancelled: true);
            return;
        }

        AdvanceNpcSessionUntilBlocked(deltaTime);
    }

    private void AdvanceNpcSessionUntilBlocked(float deltaTime)
    {
        if (_npcSession == null || !_npcSession.IsActive)
            return;

        int maxSteps = _npcSession.Interaction?.Nodes?.Count + 1 ?? 1;
        for (int i = 0; i < maxSteps; i++)
        {
            NPCInteractionNodeData currentNode = _npcSession.GetCurrentNode();
            if (currentNode == null)
            {
                FinishNpcSession(wasCancelled: false);
                return;
            }

            if (_npcSession.CurrentRunner == null)
            {
                _npcSession.CurrentRunner = _runnerFactory.Create(currentNode);
                if (_npcSession.CurrentRunner == null)
                {
                    _npcSession.CurrentNodeGuid = ResolveNextNodeGuid(currentNode, null);
                    continue;
                }

                EventComponent.Instance.Publish(new NPCInteractionNodeStartedEvent(
                    _npcSession.Target, _npcSession.NpcData, _npcSession.Interaction, currentNode));
                _npcSession.SelectedNextNodeGuid = null;
                _npcSession.CurrentRunner.Enter(_npcSession);
            }

            _npcSession.CurrentRunner.Update(_npcSession, deltaTime);
            if (!_npcSession.CurrentRunner.IsCompleted(_npcSession))
                return;

            _npcSession.CurrentRunner.Exit(_npcSession);
            _npcSession.CurrentRunner = null;
            _npcSession.CurrentNodeGuid = ResolveNextNodeGuid(currentNode, _npcSession.SelectedNextNodeGuid);
            _npcSession.SelectedNextNodeGuid = null;
        }

        Debug.LogWarning("[GameInteraction] NPC interaction advanced too many nodes in one frame and was stopped defensively.");
        FinishNpcSession(wasCancelled: true);
    }

    private void FinishNpcSession(bool wasCancelled)
    {
        if (_npcSession == null)
        {
            ReleaseNpcInput();
            return;
        }

        if (wasCancelled)
            _npcSession.Cancel();

        EventComponent.Instance.Publish(new NPCInteractionFinishedEvent(
            _npcSession.Target, _npcSession.NpcData, _npcSession.Interaction, wasCancelled));
        _npcSession = null;
        SetInteractionActive(false);
        ReleaseNpcInput();
    }

    private static string ResolveNextNodeGuid(NPCInteractionNodeData currentNode, string selectedNextNodeGuid)
    {
        if (!string.IsNullOrWhiteSpace(selectedNextNodeGuid))
            return selectedNextNodeGuid;

        if (currentNode?.Branches == null)
            return null;

        for (int i = 0; i < currentNode.Branches.Count; i++)
        {
            NPCInteractionBranchData branch = currentNode.Branches[i];
            if (branch != null && branch.IsEnabled())
                return branch.NextNodeGuid;
        }

        return null;
    }

    private void ClearPendingRequest()
    {
        RefRW<GameInteractionRequest> request = SystemAPI.GetSingletonRW<GameInteractionRequest>();
        if (request.ValueRO.HasRequest != 0)
            request.ValueRW = default;
    }

    private void SetInteractionActive(bool isActive)
    {
        EntityQuery candidateQuery = EntityManager.CreateEntityQuery(ComponentType.ReadWrite<InteractionCandidateComponent>());
        if (candidateQuery.IsEmptyIgnoreFilter)
            return;

        Entity candidateEntity = candidateQuery.GetSingletonEntity();
        InteractionCandidateComponent candidate = EntityManager.GetComponentData<InteractionCandidateComponent>(candidateEntity);
        candidate.IsInteracting = isActive ? (byte)1 : (byte)0;
        if (isActive)
        {
            candidate.Target = Entity.Null;
            candidate.Data = default;
        }

        EntityManager.SetComponentData(candidateEntity, candidate);
    }

    private bool IsInteractionActive()
    {
        EntityQuery candidateQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<InteractionCandidateComponent>());
        return !candidateQuery.IsEmptyIgnoreFilter && candidateQuery.GetSingleton<InteractionCandidateComponent>().IsInteracting != 0;
    }

    private void MarkDestroyed(Entity target)
    {
        if (EntityManager.HasComponent<DestroyEntityFlag>(target))
        {
            EntityManager.SetComponentEnabled<DestroyEntityFlag>(target, true);
            return;
        }

        EntityManager.AddComponent<DestroyEntityFlag>(target);
        EntityManager.SetComponentEnabled<DestroyEntityFlag>(target, true);
    }

    private void AcquireNpcInput()
    {
        if (_npcInputLocked)
            return;

        GameGateComponent.Instance.Lock(GameGateType.PlayerInput, NpcSessionInputLockReason);
        _npcInputLocked = true;
    }

    private void ReleaseNpcInput()
    {
        if (!_npcInputLocked)
            return;

        GameGateComponent.Instance.Unlock(GameGateType.PlayerInput, NpcSessionInputLockReason);
        _npcInputLocked = false;
    }

    private static void ShowInventoryFullTip()
    {
        UIComponent.Instance.Open<TipForm>(new TipFormOpenData
        {
            Info = LocalizationComponent.Instance.Get("ui.shop.inventory_full"),
        });
    }
}
