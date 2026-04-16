using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class NPCInteractableAuthoring : MonoBehaviour
{
    public string NPC;
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
                NPC = new FixedString64Bytes(authoring.NPC ?? string.Empty),
                interact = interactEntity,
                interactRangeSq = authoring.interactRange * authoring.interactRange,
                promptVisibleScale = interact != null ? interact.localScale.x : 1f,
            });
        }
    }
}
public struct NPCInteractable : IComponentData
{
    public FixedString64Bytes NPC;
    public Entity interact;
    public float interactRangeSq;
    public float promptVisibleScale;
}
