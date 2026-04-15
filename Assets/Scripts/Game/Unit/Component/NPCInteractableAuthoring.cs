using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class NPCInteractableAuthoring : MonoBehaviour
{
    public float interactRange = 2f;

    class NPCInteractableBaker : Baker<NPCInteractableAuthoring>
    {
        public override void Bake(NPCInteractableAuthoring authoring)
        {
            Transform interact = authoring.transform.Find("Interact");
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            Entity interactEntity = interact != null
                ? GetEntity(interact, TransformUsageFlags.Dynamic)
                : Entity.Null;
            AddComponent(entity, new NPCInteractable
            {
                interact = interactEntity,
                interactRangeSq = authoring.interactRange * authoring.interactRange,
                promptVisibleScale = interact != null ? interact.localScale.x : 1f,
            });
        }
    }
}
public struct NPCInteractable : IComponentData
{
    public Entity interact;
    public float interactRangeSq;
    public float promptVisibleScale;
}
