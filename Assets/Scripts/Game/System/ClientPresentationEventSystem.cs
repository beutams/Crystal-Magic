using CrystalMagic.Core;
using CrystalMagic.Game.Skill.Effects;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(ClientNetworkPresentationSystemGroup))]
[UpdateAfter(typeof(ClientTransformInterpolationSystem))]
public partial class ClientPresentationEventSystem : SystemBase
{
    private EntityQuery _eventQuery;

    protected override void OnCreate()
    {
        _eventQuery = GetEntityQuery(
            ComponentType.ReadWrite<ClientPresentationClockComponent>(),
            ComponentType.ReadWrite<ClientPresentationEventElement>());
    }

    protected override void OnUpdate()
    {
        if (_eventQuery.IsEmptyIgnoreFilter)
            return;

        Entity eventEntity = _eventQuery.GetSingletonEntity();
        ClientPresentationClockComponent clock =
            EntityManager.GetComponentData<ClientPresentationClockComponent>(eventEntity);
        DynamicBuffer<ClientPresentationEventElement> events =
            EntityManager.GetBuffer<ClientPresentationEventElement>(eventEntity);
        uint lastConsumedSequence = clock.LastConsumedEventSequence;
        for (int index = 0; index < events.Length; index++)
        {
            ClientPresentationEventElement presentationEvent = events[index];
            if (presentationEvent.Sequence == 0u || presentationEvent.Sequence <= lastConsumedSequence)
                continue;

            if (!ClientSkillVisualPredictionUtility.TryReconcileNetworkEvent(
                    EntityManager,
                    presentationEvent))
            {
                Play(presentationEvent);
            }
            lastConsumedSequence = presentationEvent.Sequence;
        }

        events.Clear();
        if (lastConsumedSequence != clock.LastConsumedEventSequence)
        {
            clock.LastConsumedEventSequence = lastConsumedSequence;
            EntityManager.SetComponentData(eventEntity, clock);
        }
    }

    private void Play(in ClientPresentationEventElement presentationEvent)
    {
        switch (presentationEvent.Type)
        {
            case ClientPresentationEventType.Damage:
                PlayDamage(presentationEvent);
                break;
            case ClientPresentationEventType.SpawnVfx:
                SpawnVfx(presentationEvent);
                break;
            case ClientPresentationEventType.SpawnFollowVfx:
                SpawnFollowVfx(presentationEvent);
                break;
            case ClientPresentationEventType.SpawnLineVfx:
                SpawnLineVfx(presentationEvent);
                break;
            case ClientPresentationEventType.MoveVfx:
                SpawnMoveVfx(presentationEvent);
                break;
            case ClientPresentationEventType.Sound:
                PlaySound(presentationEvent);
                break;
            case ClientPresentationEventType.CameraShake:
                PlayCameraShake(presentationEvent);
                break;
            case ClientPresentationEventType.PickupFeedback:
                PlayPickupFeedback(presentationEvent);
                break;
        }
    }

    private void PlayDamage(in ClientPresentationEventElement presentationEvent)
    {
        Entity target = presentationEvent.Target;
        if (target == Entity.Null || !EntityManager.Exists(target))
            return;

        EventComponent.Instance?.Publish(new DamageAppliedEvent(
            target,
            presentationEvent.Position,
            presentationEvent.ValueA,
            presentationEvent.FlagA != 0));
        if (!EntityManager.HasComponent<UnitVitalityComponent>(target))
            return;

        UnitVitalityComponent vitality = EntityManager.GetComponentData<UnitVitalityComponent>(target);
        EventComponent.Instance?.Publish(new UnitDamagedEvent(
            target,
            vitality.CurrentHealth,
            UnitModifierResolver.GetMaxHealth(EntityManager, target)));
    }

    private void PlayPickupFeedback(in ClientPresentationEventElement presentationEvent)
    {
        Entity target = presentationEvent.Target;
        if (target == Entity.Null || !EntityManager.Exists(target) ||
            !EntityManager.HasComponent<PlayerInputComponent>(target))
            return;

        EventComponent.Instance.Publish(new PickupFeedbackEvent(
            (PickupFeedbackType)presentationEvent.FlagA,
            presentationEvent.IntValue,
            Mathf.RoundToInt(presentationEvent.ValueA)));
    }

    private void SpawnVfx(in ClientPresentationEventElement presentationEvent)
    {
        SpriteEffectSpawnUtility.TrySpawn(
            EntityManager,
            presentationEvent.AssetName.ToString(),
            presentationEvent.Position,
            presentationEvent.Rotation,
            presentationEvent.Scale,
            presentationEvent.Duration,
            presentationEvent.FlagB != 0,
            out _);
    }

    private void SpawnFollowVfx(in ClientPresentationEventElement presentationEvent)
    {
        Entity target = presentationEvent.Target;
        if (target == Entity.Null || !EntityManager.Exists(target) ||
            !SpriteEffectSpawnUtility.TrySpawn(
                EntityManager,
                presentationEvent.AssetName.ToString(),
                presentationEvent.Position,
                presentationEvent.Rotation,
                presentationEvent.Scale,
                presentationEvent.Duration,
                out Entity effectEntity))
        {
            return;
        }

        SpriteEffectSpawnUtility.SetOrAddComponentData(
            EntityManager,
            effectEntity,
            new EffectVisualFollowComponent
            {
                Target = target,
                Offset = presentationEvent.SecondaryPosition,
                AlignRotation = presentationEvent.FlagA,
                EndWhenTargetMissing = 1,
            });
    }

    private void SpawnLineVfx(in ClientPresentationEventElement presentationEvent)
    {
        float2 direction = math.normalizesafe(presentationEvent.SecondaryPosition.xy, new float2(1f, 0f));
        float spacing = math.max(0.01f, presentationEvent.ValueB);
        int segmentCount = math.max(1, (int)math.ceil(math.max(0f, presentationEvent.ValueA) / spacing));
        for (int index = 0; index < segmentCount; index++)
        {
            float3 position = presentationEvent.Position +
                              new float3(direction.x, direction.y, 0f) * (spacing * index);
            SpriteEffectSpawnUtility.TrySpawn(
                EntityManager,
                presentationEvent.AssetName.ToString(),
                position,
                presentationEvent.Rotation,
                presentationEvent.Scale,
                presentationEvent.Duration,
                presentationEvent.FlagA == 0,
                out _);
        }
    }

    private void SpawnMoveVfx(in ClientPresentationEventElement presentationEvent)
    {
        if (!SpriteEffectSpawnUtility.TrySpawn(
                EntityManager,
                presentationEvent.AssetName.ToString(),
                presentationEvent.Position,
                presentationEvent.Rotation,
                presentationEvent.Scale,
                presentationEvent.Duration,
                presentationEvent.FlagB != 0,
                out Entity effectEntity))
        {
            return;
        }

        ClientTransformInterpolationComponent interpolation = new()
        {
            FromPosition = presentationEvent.Position,
            TargetPosition = presentationEvent.SecondaryPosition,
            FromFrame = presentationEvent.Frame,
            TargetFrame = presentationEvent.Frame,
            StartRealtime = UnityEngine.Time.realtimeSinceStartupAsDouble,
            Duration = math.max(0.001f, presentationEvent.Duration),
            Initialized = 1,
        };
        if (EntityManager.HasComponent<ClientTransformInterpolationComponent>(effectEntity))
            EntityManager.SetComponentData(effectEntity, interpolation);
        else
            EntityManager.AddComponentData(effectEntity, interpolation);
    }

    private void PlaySound(in ClientPresentationEventElement presentationEvent)
    {
        if (AudioComponent.Instance == null)
            return;

        string assetPath = presentationEvent.AssetName.ToString();
        AudioChannel channel = (AudioChannel)presentationEvent.IntValue;
        switch (channel)
        {
            case AudioChannel.BGM:
                AudioComponent.Instance.PlayBGM(assetPath, presentationEvent.ValueA);
                break;
            case AudioChannel.UI:
                AudioComponent.Instance.PlayUI(
                    assetPath,
                    presentationEvent.ValueA,
                    presentationEvent.ValueB,
                    presentationEvent.Duration);
                break;
            default:
                if (presentationEvent.FlagA != 0 && presentationEvent.Source != Entity.Null)
                {
                    AudioComponent.Instance.PlayUnitFollowEntity(
                        assetPath,
                        presentationEvent.Source,
                        EntityManager,
                        Vector3.zero,
                        presentationEvent.ValueA,
                        presentationEvent.ValueB,
                        presentationEvent.ValueC,
                        presentationEvent.Duration);
                }
                else
                {
                    AudioComponent.Instance.PlayUnit(
                        assetPath,
                        new Vector3(
                            presentationEvent.Position.x,
                            presentationEvent.Position.y,
                            presentationEvent.Position.z),
                        presentationEvent.ValueA,
                        presentationEvent.ValueB,
                        presentationEvent.ValueC,
                        presentationEvent.Duration);
                }
                break;
        }
    }

    private static void PlayCameraShake(in ClientPresentationEventElement presentationEvent)
    {
        CameraComponent.Instance?.AddShake(
            new Vector3(
                presentationEvent.Position.x,
                presentationEvent.Position.y,
                presentationEvent.Position.z),
            presentationEvent.Duration,
            presentationEvent.ValueA,
            presentationEvent.ValueB,
            presentationEvent.FlagA != 0,
            presentationEvent.ValueC);
    }
}
