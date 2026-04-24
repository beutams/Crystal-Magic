using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public sealed class UnitEditorDrawerContext
    {
        private readonly UnitEditorWindow _window;

        public UnitEditorDrawerContext(UnitEditorWindow window, GameObject prefab, string assetPath, string displayName, UnitData unit)
        {
            _window = window;
            Prefab = prefab;
            AssetPath = assetPath;
            DisplayName = displayName;
            Unit = unit;
        }

        public GameObject Prefab { get; }
        public string AssetPath { get; }
        public string DisplayName { get; }
        public UnitData Unit { get; }

        public bool HasAuthoring<T>() where T : Component
        {
            return GetAuthoring<T>() != null;
        }

        public T GetAuthoring<T>() where T : Component
        {
            return Prefab != null ? Prefab.GetComponent<T>() : null;
        }

        public void MarkPrefabDirty(Object target)
        {
            UnitEditorWindow.MarkPrefabDirty(target);
            _window?.MarkDirty();
        }
    }

    public interface IUnitEditorAttributeDrawer
    {
        bool CanDraw(UnitEditorDrawerContext context);
        void Draw(UnitEditorDrawerContext context);
    }
}
