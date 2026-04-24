using System;
using System.Collections.Generic;
using System.Reflection;
using CrystalMagic.Editor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public static class UnitEditorAttributeDrawerFactory
    {
        private readonly struct DrawerEntry
        {
            public DrawerEntry(IUnitEditorAttributeDrawer drawer, Type type, FactoryKeyAttribute mapping)
            {
                Drawer = drawer;
                Type = type;
                Mapping = mapping;
            }

            public IUnitEditorAttributeDrawer Drawer { get; }
            public Type Type { get; }
            public FactoryKeyAttribute Mapping { get; }
        }

        private static IReadOnlyList<IUnitEditorAttributeDrawer> s_drawers;

        public static IReadOnlyList<IUnitEditorAttributeDrawer> GetDrawers()
        {
            s_drawers ??= CreateDrawers();
            return s_drawers;
        }

        private static IReadOnlyList<IUnitEditorAttributeDrawer> CreateDrawers()
        {
            List<Type> drawerTypes = RegistryGeneratorUtility.CollectTypes(typeof(IUnitEditorAttributeDrawer), subclassOnly: false);
            var entries = new List<DrawerEntry>();

            foreach (Type drawerType in drawerTypes)
            {
                try
                {
                    if (Activator.CreateInstance(drawerType) is not IUnitEditorAttributeDrawer drawer)
                    {
                        continue;
                    }

                    FactoryKeyAttribute mapping = drawerType.GetCustomAttribute<FactoryKeyAttribute>(false);
                    entries.Add(new DrawerEntry(drawer, drawerType, mapping));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UnitEditorAttributeDrawerFactory] Failed to create drawer {drawerType.FullName}: {ex.Message}");
                }
            }

            entries.Sort(CompareEntries);

            var drawers = new List<IUnitEditorAttributeDrawer>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                drawers.Add(entries[i].Drawer);
            }

            return drawers;
        }

        private static int CompareEntries(DrawerEntry a, DrawerEntry b)
        {
            int orderCompare = GetOrder(a).CompareTo(GetOrder(b));
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            int keyCompare = string.Compare(GetKey(a), GetKey(b), StringComparison.Ordinal);
            if (keyCompare != 0)
            {
                return keyCompare;
            }

            return string.Compare(a.Type.FullName, b.Type.FullName, StringComparison.Ordinal);
        }

        private static int GetOrder(DrawerEntry entry)
        {
            return entry.Mapping?.Order ?? 0;
        }

        private static string GetKey(DrawerEntry entry)
        {
            return entry.Mapping?.Key ?? entry.Type.FullName ?? entry.Type.Name;
        }
    }
}
