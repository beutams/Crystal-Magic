using Unity.Entities;
using UnityEngine;

public enum UnitFactionType
{
    Protagonist = 0,
    Friendly = 1,
    Enemy = 2,
}

public class UnitFactionAuthoring : MonoBehaviour
{
    [SerializeField] private UnitFactionType _faction = UnitFactionType.Friendly;

    public UnitFactionType Faction
    {
        get => _faction;
        set => _faction = value;
    }

    class UnitFactionBaker : Baker<UnitFactionAuthoring>
    {
        public override void Bake(UnitFactionAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitFactionComponent
            {
                Value = authoring.Faction,
            });
        }
    }
}

public struct UnitFactionComponent : IComponentData
{
    public UnitFactionType Value;
}
