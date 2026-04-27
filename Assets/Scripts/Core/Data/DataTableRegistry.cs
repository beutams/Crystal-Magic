// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Use menu: Crystal Magic / Generate Data Registry
// Generated: 2026-04-15 13:35:06

using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Core
{
    public static class DataTableRegistry
    {
        public static void RegisterAll(DataComponent component)
        {
            component.LoadTable<BehaviorTreeData>("BehaviorTreeDataTable");
            component.LoadTable<EffectBuffData>("EffectBuffDataTable");
            component.LoadTable<ItemData>("ItemDataTable");
            component.LoadTable<NPCData>("NPCDataTable");
            component.LoadTable<PropertyBuffData>("PropertyBuffDataTable");
            component.LoadTable<ShopData>("ShopDataTable");
            component.LoadTable<SkillEffectData>("SkillEffectDataTable");
            component.LoadTable<SkillData>("SkillDataTable");
            component.LoadTable<UnitData>("UnitDataTable");
        }
    }
}
