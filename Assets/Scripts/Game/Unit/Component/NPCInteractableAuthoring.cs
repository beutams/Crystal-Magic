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

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            NPCData npcData = NPCAuthoringUtility.ResolveNpcData(authoring);
            AddComponent(entity, new UnitInteractableComponent
            {
                Data = new UnitInteractionData
                {
                    Kind = InteractionKind.Npc,
                    DataId = npcData?.Id ?? -1,
                },
                RangeSq = authoring.InteractRange * authoring.InteractRange,
                IsEnabled = 1,
            });
        }
    }
}
