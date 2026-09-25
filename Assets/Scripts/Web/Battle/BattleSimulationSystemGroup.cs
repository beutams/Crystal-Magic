using Server;
using Unity.Entities;
using Unity.Transforms;

// 只在联机 World 绑定 FrameManager 时创建，单机分组和更新方式保持不变。
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial class BattleSimulationSystemGroup : ComponentSystemGroup
{
    private FrameManager frame;
    private FixedStepSimulationSystemGroup physics;

    protected override void OnUpdate()
    {
        if (physics != null)
            physics.Enabled = frame.running;
        base.OnUpdate();
    }

    public static void Bind(World world, FrameManager frame)
    {
        BattleSimulationSystemGroup group = world.GetOrCreateSystemManaged<BattleSimulationSystemGroup>();
        if (group.frame == frame)
            return;

        group.frame = frame;
        group.RateManager = new BattleFrameRateManager(frame);
        // 这里只排列阶段；每个阶段内部继续由各 System 的特性排序。
        group.EnableSystemSorting = false;
        SimulationSystemGroup root = world.GetExistingSystemManaged<SimulationSystemGroup>();
        root.AddSystemToUpdateList(group);

        SystemBase[] stages =
        {
            world.GetExistingSystemManaged<FrameReceiveSystem>(),
            world.GetExistingSystemManaged<ClientInputSystemGroup>(),
            world.GetExistingSystemManaged<UnitInitializationSystemGroup>(),
            world.GetExistingSystemManaged<UnitSimulationGateSystem>(),
            world.GetExistingSystemManaged<UnitDecisionSystemGroup>(),
            world.GetExistingSystemManaged<UnitExecutionSystemGroup>(),
        };
        foreach (SystemBase stage in stages)
        {
            if (stage == null)
                continue;
            root.RemoveSystemFromUpdateList(stage);
            group.AddSystemToUpdateList(stage);
        }

        if (frame is ServerFrameManager)
        {
            group.physics = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            root.RemoveSystemFromUpdateList(group.physics);
            // 外层已经决定步长和补帧次数，物理不能再独立累积一次时间。
            group.physics.RateManager = null;
            group.AddSystemToUpdateList(group.physics);
        }
        // 客户端暂未实现玩家物理预测，不把远端单位的整个物理 World 跟着客户端追帧。

        ClientPlayerPredictionSystemGroup prediction = world.GetExistingSystemManaged<ClientPlayerPredictionSystemGroup>();
        root.RemoveSystemFromUpdateList(prediction);
        group.AddSystemToUpdateList(prediction);
        UnitPostProcessSystemGroup post = world.GetExistingSystemManaged<UnitPostProcessSystemGroup>();
        root.RemoveSystemFromUpdateList(post);
        group.AddSystemToUpdateList(post);

        if (frame is ServerFrameManager)
        {
            // 补帧时也必须每帧收集销毁消息、真正销毁，不能重复处理同一死亡实体。
            SystemHandle destroy = world.GetExistingSystem<DestroyEntitySystem>();
            world.GetExistingSystemManaged<GamePresentationSystemGroup>().RemoveSystemFromUpdateList(destroy);
            group.AddSystemToUpdateList(destroy);
        }

        root.SortSystems();
    }
}
