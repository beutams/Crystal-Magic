using Unity.Entities;

[UnitSourceProvider(typeof(GameInteractionComponent), isGlobal: true)]
public static class GameInteractionSource
{
    [UnitSourceGet(0, "world.interaction.hasPending", UnitValueCategory.Bool, UnitValueCategory.Entity,
        ParameterNames = new[] { "Target" })]
    [UnitSourceGet(1, "world.interaction.pendingActor", UnitValueCategory.Entity, UnitValueCategory.Entity,
        ParameterNames = new[] { "Target" })]
    [UnitSourceGet(2, "world.interaction.pendingRequestId", UnitValueCategory.Number, UnitValueCategory.Entity,
        ParameterNames = new[] { "Target" })]
    [UnitSourceGet(3, "world.interaction.isBusy", UnitValueCategory.Bool, UnitValueCategory.Entity,
        ParameterNames = new[] { "Actor" })]
    [UnitSourceGet(4, "world.interaction.hasResult", UnitValueCategory.Bool, UnitValueCategory.Entity,
        ParameterNames = new[] { "Actor" })]
    [UnitSourceGet(5, "world.interaction.resultCode", UnitValueCategory.Number, UnitValueCategory.Entity,
        ParameterNames = new[] { "Actor" })]
    [UnitSourceGet(6, "world.interaction.result", UnitValueCategory.Any, UnitValueCategory.Entity,
        ParameterNames = new[] { "Actor" })]
    public static bool TryGet(
        int operation,
        Entity entity,
        in ComponentLookup<GameInteractionComponent> componentLookup,
        in BufferLookup<InteractionTransactionElement> transactionLookup,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = UnitSourceValue.None;
        if (!componentLookup.HasComponent(entity) ||
            !transactionLookup.TryGetBuffer(entity, out DynamicBuffer<InteractionTransactionElement> transactions) ||
            !arguments.TryGetEntity(0, out Entity subject) || subject == Entity.Null)
            return false;

        if (operation <= 2)
        {
            for (int i = 0; i < transactions.Length; i++)
            {
                InteractionTransactionElement transaction = transactions[i];
                if (transaction.Target != subject || transaction.Phase != InteractionPhase.Pending)
                    continue;

                result = operation switch
                {
                    0 => UnitSourceValue.FromBool(true),
                    1 => UnitSourceValue.FromEntity(transaction.Actor),
                    2 => UnitSourceValue.FromInt((int)transaction.RequestId),
                    _ => UnitSourceValue.None,
                };
                return true;
            }

            if (operation == 0)
            {
                result = UnitSourceValue.FromBool(false);
                return true;
            }

            return false;
        }

        for (int i = 0; i < transactions.Length; i++)
        {
            InteractionTransactionElement transaction = transactions[i];
            if (transaction.Actor != subject)
                continue;

            bool terminal = transaction.Phase is InteractionPhase.Succeeded or InteractionPhase.Failed;
            result = operation switch
            {
                3 => UnitSourceValue.FromBool(true),
                4 => UnitSourceValue.FromBool(terminal),
                5 when terminal => UnitSourceValue.FromInt((int)transaction.ResultCode),
                6 when terminal => transaction.Result,
                _ => UnitSourceValue.None,
            };
            return result.Type != UnitValueType.None;
        }

        if (operation is 3 or 4)
        {
            result = UnitSourceValue.FromBool(false);
            return true;
        }

        return false;
    }
}
