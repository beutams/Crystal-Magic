using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(UnitMoveSystem))]
[BurstCompile]
partial struct NPCInteractPromptSystem : ISystem
{
    private NativeReference<Entity> _nearestNpc;
    private NativeReference<float> _nearestDistanceSq;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();

        _nearestNpc = new NativeReference<Entity>(Entity.Null, Allocator.Persistent);
        _nearestDistanceSq = new NativeReference<float>(float.MaxValue, Allocator.Persistent);

        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new NPCInteractionState
        {
            CurrentTarget = Entity.Null,
        });
        state.EntityManager.AddComponentData(singletonEntity, new NPCInteractionRequest
        {
            Target = Entity.Null,
            HasRequest = 0,
        });
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_nearestNpc.IsCreated)
            _nearestNpc.Dispose();

        if (_nearestDistanceSq.IsCreated)
            _nearestDistanceSq.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float3 playerPosition = float3.zero;
        bool hasPlayer = false;

        foreach ((RefRO<PlayerTag> _, RefRO<LocalTransform> transform) in
            SystemAPI.Query<RefRO<PlayerTag>, RefRO<LocalTransform>>())
        {
            playerPosition = transform.ValueRO.Position;
            hasPlayer = true;
            break;
        }

        _nearestNpc.Value = Entity.Null;
        _nearestDistanceSq.Value = float.MaxValue;

        state.Dependency = new NPCInteractPromptFindJob
        {
            HasPlayer = hasPlayer,
            PlayerPosition = playerPosition,
            LocalTransforms = SystemAPI.GetComponentLookup<LocalTransform>(false),
            NearestNpc = _nearestNpc,
            NearestDistanceSq = _nearestDistanceSq,
        }.Schedule(state.Dependency);
        state.Dependency.Complete();

        Entity nearestNpc = _nearestNpc.Value;

        state.Dependency = new NPCInteractPromptShowNearestJob
        {
            NearestNpc = nearestNpc,
            Interactables = SystemAPI.GetComponentLookup<NPCInteractable>(true),
            LocalTransforms = SystemAPI.GetComponentLookup<LocalTransform>(false),
        }.Schedule(state.Dependency);
        state.Dependency.Complete();

        RefRW<NPCInteractionState> interactionState = SystemAPI.GetSingletonRW<NPCInteractionState>();
        interactionState.ValueRW.CurrentTarget = nearestNpc;
    }
}

[BurstCompile]
public partial struct NPCInteractPromptFindJob : IJobEntity
{
    public bool HasPlayer;
    public float3 PlayerPosition;
    public ComponentLookup<LocalTransform> LocalTransforms;
    public NativeReference<Entity> NearestNpc;
    public NativeReference<float> NearestDistanceSq;

    public void Execute(Entity entity, in NPCTag tag, in NPCInteractable interactable)
    {
        HidePrompt(interactable);

        if (!HasPlayer || !LocalTransforms.HasComponent(entity))
            return;

        float3 npcPosition = LocalTransforms[entity].Position;
        float distanceSq = math.distancesq(PlayerPosition, npcPosition);
        if (distanceSq > interactable.interactRangeSq || distanceSq >= NearestDistanceSq.Value)
            return;

        NearestDistanceSq.Value = distanceSq;
        NearestNpc.Value = entity;
    }

    private void HidePrompt(NPCInteractable interactable)
    {
        if (interactable.interact == Entity.Null || !LocalTransforms.HasComponent(interactable.interact))
            return;

        LocalTransform interactTransform = LocalTransforms[interactable.interact];
        interactTransform.Scale = 0f;
        LocalTransforms[interactable.interact] = interactTransform;
    }
}

[BurstCompile]
public struct NPCInteractPromptShowNearestJob : IJob
{
    public Entity NearestNpc;
    [ReadOnly] public ComponentLookup<NPCInteractable> Interactables;
    public ComponentLookup<LocalTransform> LocalTransforms;

    public void Execute()
    {
        if (NearestNpc == Entity.Null || !Interactables.HasComponent(NearestNpc))
            return;

        NPCInteractable interactable = Interactables[NearestNpc];
        if (interactable.interact == Entity.Null || !LocalTransforms.HasComponent(interactable.interact))
            return;

        LocalTransform interactTransform = LocalTransforms[interactable.interact];
        interactTransform.Scale = interactable.promptVisibleScale;
        LocalTransforms[interactable.interact] = interactTransform;
    }
}
