using System;
using Unity.Collections;
using Unity.Entities;

[Serializable]
public sealed class NetworkPresentationEventStateData : NetworkStateData
{
    public uint sequence;
    public ClientPresentationEventType eventType;
    public Guid sourceUnitId;
    public Guid targetUnitId;
    public string assetName;
    public float positionX;
    public float positionY;
    public float positionZ;
    public float secondaryX;
    public float secondaryY;
    public float secondaryZ;
    public float rotationX;
    public float rotationY;
    public float rotationZ;
    public float rotationW = 1f;
    public float scale = 1f;
    public float duration;
    public float valueA;
    public float valueB;
    public float valueC;
    public int intValue;
    public byte flagA;
    public byte flagB;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.IsClient)
            return;

        context.TryGetEntity(sourceUnitId, out Entity source);
        context.TryGetEntity(targetUnitId, out Entity target);
        Entity presentationEntity = context.GetOrCreateClientPresentationEntity();
        if (!context.EntityManager.HasBuffer<ClientPresentationEventElement>(presentationEntity))
            context.EntityManager.AddBuffer<ClientPresentationEventElement>(presentationEntity);

        DynamicBuffer<ClientPresentationEventElement> events =
            context.EntityManager.GetBuffer<ClientPresentationEventElement>(presentationEntity);
        events.Add(new ClientPresentationEventElement
        {
            Frame = context.Frame,
            Sequence = sequence,
            Type = eventType,
            Source = source,
            Target = target,
            AssetName = new FixedString128Bytes(assetName ?? string.Empty),
            Position = new Unity.Mathematics.float3(positionX, positionY, positionZ),
            SecondaryPosition = new Unity.Mathematics.float3(secondaryX, secondaryY, secondaryZ),
            Rotation = new Unity.Mathematics.quaternion(rotationX, rotationY, rotationZ, rotationW),
            Scale = scale,
            Duration = duration,
            ValueA = valueA,
            ValueB = valueB,
            ValueC = valueC,
            IntValue = intValue,
            FlagA = flagA,
            FlagB = flagB,
        });
    }
}
