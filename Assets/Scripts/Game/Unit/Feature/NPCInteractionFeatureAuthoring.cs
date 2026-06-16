using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NPCInteractionFeatureAuthoring : MonoBehaviour
{
    [SerializeField, HideInInspector] private float _interactRange = 2f;

    public float InteractRange
    {
        get => _interactRange;
        set => _interactRange = value;
    }

    private sealed class Baker : Unity.Entities.Baker<NPCInteractionFeatureAuthoring>
    {
        public override void Bake(NPCInteractionFeatureAuthoring authoring)
        {
            Entity entity = this.GetFeatureEntity();
            this.AddNpcInteractionComponents(authoring, entity, Mathf.Max(0f, authoring.InteractRange));
        }
    }
}
