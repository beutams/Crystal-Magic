using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Core
{
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GamePresentationSystemGroup))]
    public partial class DungeonFogOfWarSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            RuntimeDungeonFogData fogData = GameRuntimeStateUtility.GetDungeonRuntimeMap()?.FogData;
            if (fogData == null)
                return;

            UpdatePlayerVisibility(fogData);
            fogData.UpdateVisual(SystemAPI.Time.DeltaTime);
            UpdateHostileVisuals(fogData);
        }

        private void UpdatePlayerVisibility(RuntimeDungeonFogData fogData)
        {
            foreach ((RefRO<UnitFactionComponent> faction, RefRO<LocalTransform> transform) in
                     SystemAPI.Query<RefRO<UnitFactionComponent>, RefRO<LocalTransform>>())
            {
                if (!UnitFactionUtility.IsPlayer(faction.ValueRO.Value))
                    continue;

                Vector3 position = new(
                    transform.ValueRO.Position.x,
                    transform.ValueRO.Position.y,
                    transform.ValueRO.Position.z);
                if (fogData.TryGetCell(position, out Vector2Int playerCell))
                    fogData.UpdateVisibility(playerCell);

                return;
            }
        }

        private void UpdateHostileVisuals(RuntimeDungeonFogData fogData)
        {
            foreach ((RefRO<UnitAnimationComponent> _, RefRO<UnitFactionComponent> faction,
                      RefRO<LocalTransform> transform, Entity entity) in
                     SystemAPI.Query<RefRO<UnitAnimationComponent>, RefRO<UnitFactionComponent>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (!UnitFactionUtility.IsHostile(faction.ValueRO.Value) ||
                    !EntityManager.HasComponent<SpriteRenderer>(entity))
                {
                    continue;
                }

                SpriteRenderer spriteRenderer = EntityManager.GetComponentObject<SpriteRenderer>(entity);

                Vector3 position = new(
                    transform.ValueRO.Position.x,
                    transform.ValueRO.Position.y,
                    transform.ValueRO.Position.z);
                bool shouldShow = fogData.TryGetCell(position, out Vector2Int cell) &&
                                  fogData.IsVisible(cell.x, cell.y);
                if (spriteRenderer.enabled != shouldShow)
                    spriteRenderer.enabled = shouldShow;
            }
        }
    }
}
