using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitIntentAuthoring : MonoBehaviour
{
    class UnitIntentBaker : Baker<UnitIntentAuthoring>
    {
        public override void Bake(UnitIntentAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitIntentComponent());
            AddComponent(entity, UnitCastAvailabilityComponent.CreateDefault());
        }
    }
}

public struct UnitIntentComponent : IComponentData
{
    //移动
    public float2 MoveDirection;
    //技能释放
    public bool WantToCast;
    public float2 CastTargetPosition;
    public UnitSkillSelectionMode SkillRequestMode;
    public int RequestedSkillId;
    public int RequestedTagMask;
    public bool WantToInteract;
    public bool WantToUseProp;
    public int RequestedPropShortcutIndex;

    public void ClearFrameIntent()
    {
        MoveDirection = float2.zero;
        WantToCast = false;
        CastTargetPosition = float2.zero;
        SkillRequestMode = UnitSkillSelectionMode.None;
        RequestedSkillId = -1;
        RequestedTagMask = 0;
        WantToInteract = false;
        WantToUseProp = false;
        RequestedPropShortcutIndex = -1;
    }
}
