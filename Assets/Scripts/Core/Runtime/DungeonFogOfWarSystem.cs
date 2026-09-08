using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Core
{
    [UpdateInGroup(typeof(UnitPostProcessSystemGroup))]
    public partial class DungeonFogOfWarSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            RuntimeDungeonFogData fogData = RuntimeDataComponent.Instance.GetDungeonMapData().FogData;
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
            foreach ((UnitAnimationComponent animation, RefRO<UnitFactionComponent> faction, RefRO<LocalTransform> transform) in
                     SystemAPI.Query<UnitAnimationComponent, RefRO<UnitFactionComponent>, RefRO<LocalTransform>>())
            {
                if (!UnitFactionUtility.IsHostile(faction.ValueRO.Value) || animation.Renderer == null)
                    continue;

                Vector3 position = new(
                    transform.ValueRO.Position.x,
                    transform.ValueRO.Position.y,
                    transform.ValueRO.Position.z);
                bool shouldShow = fogData.TryGetCell(position, out Vector2Int cell) &&
                                  fogData.IsVisible(cell.x, cell.y);
                if (animation.Renderer.enabled != shouldShow)
                    animation.Renderer.enabled = shouldShow;
            }
        }
    }
}
