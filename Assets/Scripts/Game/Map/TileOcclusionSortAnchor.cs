using UnityEngine;
using UnityEngine.Rendering;

namespace CrystalMagic.Game.Map
{
    // A local visual component on an editor-generated obstacle, not a global service.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SortingGroup))]
    public sealed class TileOcclusionSortAnchor : MonoBehaviour
    {
        public const float SortingPrecision = 100f;
        public const int MinimumOrder = -29999;
        public const int MaximumOrder = 29999;
        private SortingGroup _group;

        public static int OrderForY(float worldY) =>
            Mathf.Clamp(Mathf.RoundToInt(-worldY * SortingPrecision), MinimumOrder, MaximumOrder);

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (_group == null)
                _group = GetComponent<SortingGroup>();
            int order = OrderForY(transform.position.y);
            if (_group.sortingOrder != order)
                _group.sortingOrder = order;
        }
    }
}
