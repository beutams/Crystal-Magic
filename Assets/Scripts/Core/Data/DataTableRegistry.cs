// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Use menu: Crystal Magic / Generate Data Registry
// Generated: 2026-04-08 11:15:34

using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Core
{
    public static class DataTableRegistry
    {
        public static void RegisterAll(DataComponent component)
        {
            component.LoadTable<ItemData>("ItemDataTable");
            component.LoadTable<PropertyBuffData>("PropertyBuffDataTable");
            component.LoadTable<SkillData>("SkillDataTable");
            component.LoadTable<EffectBuffData>("TickEffectBuffDataTable");
            component.LoadTable<UnitData>("UnitDataTable");
        }
    }
}
