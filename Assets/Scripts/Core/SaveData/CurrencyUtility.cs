namespace CrystalMagic.Core
{
    public static class CurrencyUtility
    {
        public static void AddMoneyToCurrentArea(long amount)
        {
            if (amount <= 0 || SaveDataComponent.Instance == null)
                return;

            SaveLocationData locationData = SaveDataComponent.Instance.GetLocationData();
            if (locationData != null && locationData.AreaType == SaveAreaType.Dungeon)
            {
                DungeonRunData dungeonRunData = SaveDataComponent.Instance.GetDungeonRunData();
                if (dungeonRunData != null)
                    dungeonRunData.RunMoney += amount;
            }
            else
            {
                TownData townData = SaveDataComponent.Instance.GetTownData();
                if (townData != null)
                    townData.StashMoney += amount;
            }

            SaveDataComponent.Instance.NotifyBackpackDataChanged();
        }
    }
}
