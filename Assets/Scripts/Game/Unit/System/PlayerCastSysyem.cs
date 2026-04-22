using Unity.Entities;

partial class PlayerCastSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach(var (_,intent) in SystemAPI.Query<RefRO<PlayerTag>,RefRO<UnitIntentComponent>>())
        {
            if(intent.ValueRO.WantToCast)
            {

            }
        }
    }
}
