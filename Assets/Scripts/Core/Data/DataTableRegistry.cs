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
            component.LoadTable<BuffData>("BuffDataTable");
            component.LoadTable<DungeonThemeData>("DungeonThemeDataTable");
            component.LoadTable<DropData>("DropDataTable");
            component.LoadTable<EquipData>("EquipDataTable");
            component.LoadTable<ItemData>("ItemDataTable");
            component.LoadTable<LocalizationData>("LocalizationDataTable");
            component.LoadTable<PropData>("PropDataTable");
            component.LoadTable<NPCData>("NPCDataTable");
            component.LoadTable<ShopData>("ShopDataTable");
            component.LoadTable<SkillAdditionData>("SkillAdditionDataTable");
            component.LoadTable<SkillData>("SkillDataTable");
            component.LoadTable<StateScriptData>("StateScriptDataTable");
            component.LoadTable<UnitAnimationProfileData>("UnitAnimationProfileDataTable");
            component.LoadTable<UnitData>("UnitDataTable");
        }
    }
}
