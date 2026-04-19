using Unity.Entities;
using UnityEngine;

public class NPCInteractableAuthoring : MonoBehaviour
{
    [SerializeField, HideInInspector] private int _npcDataId;
    [SerializeField, HideInInspector] private float _interactRange = 2f;

    public int NpcDataId
    {
        get => _npcDataId;
        set => _npcDataId = value;
    }

    public float InteractRange
    {
        get => _interactRange;
        set => _interactRange = value;
    }

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
                NpcDataId = authoring.NpcDataId,
                interact = interactEntity,
                interactRangeSq = authoring.InteractRange * authoring.InteractRange,
                promptVisibleScale = interact != null ? interact.localScale.x : 1f,
            });
        }
    }
}

public struct NPCInteractable : IComponentData
{
    public int NpcDataId;
    public Entity interact;
    public float interactRangeSq;
    public float promptVisibleScale;
}
