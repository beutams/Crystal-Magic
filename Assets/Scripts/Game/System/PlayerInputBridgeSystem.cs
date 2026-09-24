using CrystalMagic.Core;
using CrystalMagic.Game;
using Server;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientInputSystemGroup))]
public partial class PlayerInputBridgeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        InputState inputState = InputComponent.Instance.CurrentState;
        GameWorldRole role = GameWorldContextUtility.Get(EntityManager).Role;
        ClientFrameManager clientFrame = null;
        if (role == GameWorldRole.Client)
            FrameManagerUtility.TryGet(EntityManager, out clientFrame);

        using (NativeArray<Entity> missingBuffers = SystemAPI.QueryBuilder()
                   .WithAll<PlayerInputComponent>().WithNone<PlayerInputEventElement>()
                   .Build().ToEntityArray(Allocator.Temp))
        {
            foreach (Entity player in missingBuffers)
                EntityManager.AddBuffer<PlayerInputEventElement>(player);
        }

        using (NativeArray<Entity> inputEntities = SystemAPI.QueryBuilder()
                   .WithAll<PlayerInputComponent>()
                   .Build().ToEntityArray(Allocator.Temp))
        {
            if (inputEntities.Length == 0)
                return;

            // 本地/客户端 World 只有一个可输入玩家；避免持有 Query 迭代器时执行道具效果，
            // 因为效果队列首次创建时可能发生结构变化。
            Entity entity = inputEntities[0];
            PlayerInputComponent oldInput = EntityManager.GetComponentData<PlayerInputComponent>(entity);
            PlayerInputComponent input = oldInput;
            BattlePlayerStatusComponent status = EntityManager.HasComponent<BattlePlayerStatusComponent>(entity)
                ? EntityManager.GetComponentData<BattlePlayerStatusComponent>(entity)
                : default;
            bool transitionWaiting = status.IsWaitingForTransition;
            // 观战只禁止战斗操作，仍然保留移动。
            input.Move = transitionWaiting ? float2.zero : new float2(inputState.Move.x, inputState.Move.y);
            input.PointerWorldPosition = new float3(
                inputState.PointerWorldPosition.x,
                inputState.PointerWorldPosition.y,
                inputState.PointerWorldPosition.z);
            input.ContinuousPrimaryHeld = !status.IsInputLocked && inputState.IsPrimaryHeld ? (byte)1 : (byte)0;
            input.IsPrimaryHeld = input.ContinuousPrimaryHeld;
            input.IsInteractHeld = 0;
            input.IsInventoryHeld = !status.IsInputLocked && inputState.IsInventoryHeld ? (byte)1 : (byte)0;
            input.IsPropertyHeld = !status.IsInputLocked && inputState.IsPropertyHeld ? (byte)1 : (byte)0;
            input.IsEscapeHeld = !status.IsInputLocked && inputState.IsEscapeHeld ? (byte)1 : (byte)0;
            input.IsSkillHeld = 0;
            input.IsUsePropHeld = 0;
            input.PropIndex = -1;

            while (InputComponent.Instance.TryDequeueBattleOperation(out PlayerInputOperation operation))
            {
                if (status.IsInputLocked)
                    continue;

                switch (operation.Type)
                {
                    case PlayerInputOperationType.PrimaryPressed:
                        if (role == GameWorldRole.Client)
                        {
                            clientFrame?.EnqueueOperation(new NetworkPrimaryPressData
                            {
                                pointerX = operation.PointerWorldPosition.x,
                                pointerY = operation.PointerWorldPosition.y,
                                pointerZ = operation.PointerWorldPosition.z,
                            });
                        }
                        PlayerInputComponent pressInput = input;
                        pressInput.PointerWorldPosition = new float3(
                            operation.PointerWorldPosition.x,
                            operation.PointerWorldPosition.y,
                            operation.PointerWorldPosition.z);
                        PlayerInputEventUtility.Append(EntityManager, entity, operation.Type, pressInput);
                        break;

                    case PlayerInputOperationType.Interact:
                        // 客户端的出口选择界面仍由本地状态脚本打开。
                        PlayerInputEventUtility.Append(EntityManager, entity, operation.Type, input);
                        if (role == GameWorldRole.Client)
                            clientFrame?.EnqueueOperation(new NetworkInteractData());
                        break;

                    case PlayerInputOperationType.SelectSkillChain:
                        if (operation.IntValue < 0 || operation.IntValue >= 5)
                            break;

                        if (role == GameWorldRole.Client)
                        {
                            if (clientFrame?.EnqueueOperation(new NetworkSkillChainSelectData
                            {
                                skillChainIndex = operation.IntValue,
                            }) != true)
                                break;
                        }

                        input.SkillChainIndex = operation.IntValue;
                        PlayerInputEventUtility.Append(EntityManager, entity, operation.Type, input);
                        break;

                    case PlayerInputOperationType.UseProp:
                        if (role == GameWorldRole.Client)
                        {
                            if (clientFrame?.EnqueueOperation(new NetworkPropUseData
                            {
                                slotIndex = operation.IntValue,
                                itemId = operation.ItemId,
                            }) != true)
                                break;
                        }
                        else if (PropUseUtility.TryBuildContext(
                                     EntityManager,
                                     entity,
                                     out PropUseRequestContext context,
                                     out _))
                        {
                            PropUseUtility.TryApplyQueuedPropSlot(
                                operation.IntValue,
                                operation.ItemId,
                                context,
                                out _);
                        }
                        PlayerInputComponent propInput = input;
                        propInput.PropIndex = operation.IntValue;
                        PlayerInputEventUtility.Append(EntityManager, entity, operation.Type, propInput);
                        break;
                }
            }

            bool inputChanged = !oldInput.Move.Equals(input.Move) ||
                                !oldInput.PointerWorldPosition.Equals(input.PointerWorldPosition) ||
                                oldInput.IsPrimaryHeld != input.IsPrimaryHeld ||
                                oldInput.ContinuousPrimaryHeld != input.ContinuousPrimaryHeld ||
                                oldInput.IsInteractHeld != input.IsInteractHeld ||
                                oldInput.IsInventoryHeld != input.IsInventoryHeld ||
                                oldInput.IsPropertyHeld != input.IsPropertyHeld ||
                                oldInput.IsEscapeHeld != input.IsEscapeHeld ||
                                oldInput.IsSkillHeld != input.IsSkillHeld ||
                                oldInput.SkillChainIndex != input.SkillChainIndex ||
                                oldInput.IsUsePropHeld != input.IsUsePropHeld ||
                                oldInput.PropIndex != input.PropIndex;
            input.NetworkDirty = inputChanged ? (byte)1 : oldInput.NetworkDirty;
            EntityManager.SetComponentData(entity, input);

            if (oldInput.SkillChainIndex != input.SkillChainIndex)
                EventComponent.Instance.Publish(new CommonGameEvent(PlayerInputComponent.SkillChainChangedEventName));
        }
    }
}
