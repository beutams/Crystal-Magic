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

        public T GetModule<T>() where T : UnitModuleData
        {
            return Unit?.GetModule<T>();
        }

        public T GetOrCreateModule<T>() where T : UnitModuleData, new()
        {
            return Unit?.GetOrCreateModule<T>();
        }

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

        public DropData GetDropData(int dropDataId)
        {
            return _window?.GetDropData(dropDataId);
        }

        public DropData CreateDropDataForUnit(UnitDropModuleData module)
        {
            return _window?.CreateDropDataForUnit(Unit, module);
        }

        public bool DrawInlineDropDataEditor(UnitDropModuleData module)
        {
            return _window != null && _window.DrawInlineDropDataEditor(Unit, module);
        }
    }

    public interface IUnitEditorAttributeDrawer
    {
        bool CanDraw(UnitEditorDrawerContext context);
        void Draw(UnitEditorDrawerContext context);
    }
}
