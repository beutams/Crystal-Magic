using System;
using System.Collections.Generic;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Editor.Data;
using UnityEngine;

namespace CrystalMagic.Editor.Unit
{
    public static class UnitRuntimeAttributeDrawerFactory
    {
        private readonly struct DrawerEntry
        {
            public DrawerEntry(IUnitRuntimeAttributeDrawer drawer, Type type, FactoryKeyAttribute mapping)
            {
                Drawer = drawer;
                Type = type;
                Mapping = mapping;
            }

            public IUnitRuntimeAttributeDrawer Drawer { get; }
            public Type Type { get; }
            public FactoryKeyAttribute Mapping { get; }
        }

        private static IReadOnlyList<IUnitRuntimeAttributeDrawer> s_drawers;

        public static IReadOnlyList<IUnitRuntimeAttributeDrawer> GetDrawers()
        {
            s_drawers ??= CreateDrawers();
            return s_drawers;
        }

        private static IReadOnlyList<IUnitRuntimeAttributeDrawer> CreateDrawers()
        {
            List<Type> drawerTypes = RegistryGeneratorUtility.CollectTypes(typeof(IUnitRuntimeAttributeDrawer), subclassOnly: false);
            var entries = new List<DrawerEntry>();

            foreach (Type drawerType in drawerTypes)
            {
                try
                {
                    if (Activator.CreateInstance(drawerType) is not IUnitRuntimeAttributeDrawer drawer)
                        continue;

                    FactoryKeyAttribute mapping = drawerType.GetCustomAttribute<FactoryKeyAttribute>(false);
                    entries.Add(new DrawerEntry(drawer, drawerType, mapping));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UnitRuntimeAttributeDrawerFactory] Failed to create drawer {drawerType.FullName}: {ex.Message}");
                }
            }

            entries.Sort(CompareEntries);

            var drawers = new List<IUnitRuntimeAttributeDrawer>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
                drawers.Add(entries[i].Drawer);

            return drawers;
        }

        private static int CompareEntries(DrawerEntry a, DrawerEntry b)
        {
            int orderCompare = GetOrder(a).CompareTo(GetOrder(b));
            if (orderCompare != 0)
                return orderCompare;

            int keyCompare = string.Compare(GetKey(a), GetKey(b), StringComparison.Ordinal);
            if (keyCompare != 0)
                return keyCompare;

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
