using Unity.Entities;
using UnityEngine;
using CrystalMagic.Game.Data;

public class NPCInteractableAuthoring : MonoBehaviour
{
    [SerializeField, HideInInspector] private float _interactRange = 2f;

    public float InteractRange
    {
        get => _interactRange;
        set => _interactRange = value;
    }

    class NPCInteractableBaker : Baker<NPCInteractableAuthoring>
    {
        public override void Bake(NPCInteractableAuthoring authoring)
        {
#if UNITY_EDITOR
            DependsOn(NPCAuthoringUtility.GetNpcDataTableAsset());
#endif

            Transform interact = authoring.transform.Find("Interact");
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            Entity interactEntity = interact != null
                ? GetEntity(interact, TransformUsageFlags.Dynamic)
                : Entity.Null;
            NPCData npcData = NPCAuthoringUtility.ResolveNpcData(authoring);
            AddComponent(entity, new NPCInteractable
            {
                NpcId = npcData?.Id ?? -1,
                interact = interactEntity,
                interactRangeSq = authoring.InteractRange * authoring.InteractRange,
            });
        }
    }
}

public struct NPCInteractable : IComponentData
{
    public int NpcId;
    public Entity interact;
    public float interactRangeSq;
}
