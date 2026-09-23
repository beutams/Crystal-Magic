using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;

public static class GameInteractionUtility
{
    public static bool TryRequest(
        EntityManager entityManager,
        Entity runtimeEntity,
        Entity actor,
        Entity target)
    {
        if (!IsRuntimeValid(entityManager, runtimeEntity) || actor == Entity.Null || !entityManager.Exists(actor))
            return false;

        DynamicBuffer<InteractionTransactionElement> transactions =
            entityManager.GetBuffer<InteractionTransactionElement>(runtimeEntity);
        if (FindByActor(transactions, actor, includeCompleted: true) >= 0)
            return false;

        InteractionResultCode failure = ValidateTarget(entityManager, actor, target);
        if (failure == InteractionResultCode.Success && FindByTarget(transactions, target, includeCompleted: false) >= 0)
            failure = InteractionResultCode.Busy;

        GameInteractionComponent runtime = entityManager.GetComponentData<GameInteractionComponent>(runtimeEntity);
        runtime.NextRequestId++;
        if (runtime.NextRequestId == 0)
            runtime.NextRequestId = 1;
        entityManager.SetComponentData(runtimeEntity, runtime);

        transactions.Add(new InteractionTransactionElement
        {
            RequestId = runtime.NextRequestId,
            Actor = actor,
            Target = target,
            Phase = failure == InteractionResultCode.Success
                ? InteractionPhase.Pending
                : InteractionPhase.Failed,
            ResultCode = failure == InteractionResultCode.Success
                ? InteractionResultCode.None
                : failure,
        });
        return true;
    }

    public static bool TryBegin(
        EntityManager entityManager,
        Entity runtimeEntity,
        Entity target,
        out InteractionTransactionElement transaction)
    {
        transaction = default;
        if (!TryGetTransactions(entityManager, runtimeEntity, out DynamicBuffer<InteractionTransactionElement> transactions))
            return false;

        int index = FindByTarget(transactions, target, includeCompleted: false);
        if (index < 0 || transactions[index].Phase != InteractionPhase.Pending)
            return false;

        transaction = transactions[index];
        transaction.Phase = InteractionPhase.Processing;
        transactions[index] = transaction;
        return true;
    }

    public static bool TryGetPending(
        EntityManager entityManager,
        Entity runtimeEntity,
        Entity target,
        out InteractionTransactionElement transaction)
    {
        transaction = default;
        if (!TryGetTransactions(entityManager, runtimeEntity, out DynamicBuffer<InteractionTransactionElement> transactions))
            return false;

        int index = FindByTarget(transactions, target, includeCompleted: false);
        if (index < 0 || transactions[index].Phase != InteractionPhase.Pending)
            return false;

        transaction = transactions[index];
        return true;
    }

    public static bool Complete(
        EntityManager entityManager,
        Entity runtimeEntity,
        Entity target,
        InteractionResultCode resultCode,
        in UnitSourceValue result)
    {
        if (!TryGetTransactions(entityManager, runtimeEntity, out DynamicBuffer<InteractionTransactionElement> transactions))
            return false;

        int index = FindByTarget(transactions, target, includeCompleted: false);
        if (index < 0)
            return false;

        InteractionTransactionElement transaction = transactions[index];
        transaction.Phase = resultCode == InteractionResultCode.Success
            ? InteractionPhase.Succeeded
            : InteractionPhase.Failed;
        transaction.ResultCode = resultCode;
        transaction.Result = result;
        transactions[index] = transaction;
        return true;
    }

    public static bool Acknowledge(
        EntityManager entityManager,
        Entity runtimeEntity,
        Entity actor)
    {
        if (!TryGetTransactions(entityManager, runtimeEntity, out DynamicBuffer<InteractionTransactionElement> transactions))
            return false;

        int index = FindByActor(transactions, actor, includeCompleted: true);
        if (index < 0 || !IsCompleted(transactions[index].Phase))
            return false;

        transactions.RemoveAtSwapBack(index);
        return true;
    }

    public static void FailTarget(
        EntityManager entityManager,
        Entity runtimeEntity,
        Entity target,
        InteractionResultCode resultCode = InteractionResultCode.Cancelled)
    {
        if (!TryGetTransactions(entityManager, runtimeEntity, out DynamicBuffer<InteractionTransactionElement> transactions))
            return;

        int index = FindByTarget(transactions, target, includeCompleted: false);
        if (index < 0)
            return;

        InteractionTransactionElement transaction = transactions[index];
        transaction.Phase = InteractionPhase.Failed;
        transaction.ResultCode = resultCode;
        transaction.Result = UnitSourceValue.None;
        transactions[index] = transaction;
    }

    private static InteractionResultCode ValidateTarget(EntityManager entityManager, Entity actor, Entity target)
    {
        if (target == Entity.Null || !entityManager.Exists(target) ||
            !entityManager.HasComponent<UnitInteractableComponent>(target))
        {
            return InteractionResultCode.InvalidTarget;
        }

        UnitInteractableComponent interactable = entityManager.GetComponentData<UnitInteractableComponent>(target);
        if (!GameInteractionTargetUtility.IsAvailable(entityManager, target, interactable))
            return InteractionResultCode.InvalidTarget;

        if (interactable.RangeSq <= 0f ||
            !entityManager.HasComponent<LocalTransform>(actor) ||
            !entityManager.HasComponent<LocalTransform>(target))
        {
            return InteractionResultCode.Success;
        }

        float3 actorPosition = entityManager.GetComponentData<LocalTransform>(actor).Position;
        float3 targetPosition = entityManager.GetComponentData<LocalTransform>(target).Position;
        return math.lengthsq((actorPosition - targetPosition).xy) <= interactable.RangeSq
            ? InteractionResultCode.Success
            : InteractionResultCode.InvalidTarget;
    }

    private static bool TryGetTransactions(
        EntityManager entityManager,
        Entity runtimeEntity,
        out DynamicBuffer<InteractionTransactionElement> transactions)
    {
        transactions = default;
        if (!IsRuntimeValid(entityManager, runtimeEntity))
            return false;

        transactions = entityManager.GetBuffer<InteractionTransactionElement>(runtimeEntity);
        return true;
    }

    private static bool IsRuntimeValid(EntityManager entityManager, Entity runtimeEntity)
    {
        return runtimeEntity != Entity.Null && entityManager.Exists(runtimeEntity) &&
               entityManager.HasComponent<GameInteractionComponent>(runtimeEntity) &&
               entityManager.HasBuffer<InteractionTransactionElement>(runtimeEntity);
    }

    private static int FindByActor(
        in DynamicBuffer<InteractionTransactionElement> transactions,
        Entity actor,
        bool includeCompleted)
    {
        for (int index = 0; index < transactions.Length; index++)
        {
            InteractionTransactionElement transaction = transactions[index];
            if (transaction.Actor == actor && (includeCompleted || !IsCompleted(transaction.Phase)))
                return index;
        }

        return -1;
    }

    private static int FindByTarget(
        in DynamicBuffer<InteractionTransactionElement> transactions,
        Entity target,
        bool includeCompleted)
    {
        for (int index = 0; index < transactions.Length; index++)
        {
            InteractionTransactionElement transaction = transactions[index];
            if (transaction.Target == target && (includeCompleted || !IsCompleted(transaction.Phase)))
                return index;
        }

        return -1;
    }

    private static bool IsCompleted(InteractionPhase phase)
    {
        return phase is InteractionPhase.Succeeded or InteractionPhase.Failed;
    }
}
