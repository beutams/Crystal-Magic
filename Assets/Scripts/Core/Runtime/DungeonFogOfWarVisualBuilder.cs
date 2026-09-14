using UnityEngine;

namespace CrystalMagic.Core
{
    internal static class DungeonFogOfWarVisualBuilder
    {
        private const int FogSortingOrder = 32000;

        public static void Build(DungeonSceneRuntimeRoot runtimeRoot, RuntimeDungeonFogData fogData)
        {
            if (runtimeRoot == null || fogData == null)
                return;

            fogData.CreateVisualAssets();
            GameObject fogObject = new("FogOfWar");
            fogObject.transform.SetParent(runtimeRoot.transform, false);
            fogObject.transform.position = new Vector3(
                fogData.WorldOrigin.x + fogData.Width * fogData.CellWorldSize * 0.5f,
                fogData.WorldOrigin.y + fogData.Height * fogData.CellWorldSize * 0.5f,
                0f);

            SpriteRenderer renderer = fogObject.AddComponent<SpriteRenderer>();
            renderer.sprite = fogData.WorldSprite;
            renderer.sortingOrder = FogSortingOrder;
            runtimeRoot.TrackRuntimeAssets(fogData.Texture, fogData.WorldSprite, fogData.MinimapSprite);
        }
    }
}
