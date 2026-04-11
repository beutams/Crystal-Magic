using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class NPCInteractableAuthoring : MonoBehaviour
{
    class NPCInteractableBaker : Baker<NPCInteractableAuthoring>
    {
        public override void Bake(NPCInteractableAuthoring authoring)
        {
            Transform interact = authoring.transform.Find("Interact");
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            Entity interactEntity = GetEntity(interact, TransformUsageFlags.Dynamic);
            AddComponent(entity, new NPCInteractable { interact = interactEntity });
        }
    }
}
public struct NPCInteractable : IComponentData
{
    public Entity interact;
}
