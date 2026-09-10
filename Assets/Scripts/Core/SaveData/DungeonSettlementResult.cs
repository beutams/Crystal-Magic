namespace CrystalMagic.Core
{
    public enum DungeonSettlementOutcome
    {
        Escaped,
        Defeated,
    }

    public sealed class DungeonSettlementResult
    {
        public DungeonSettlementOutcome Outcome { get; set; }
        public int ReachedFloor { get; set; }
        public long ReturnedMoney { get; set; }
        public int GainedItemQuantity { get; set; }
        public int LostItemQuantity { get; set; }
        public int NonTransferableItemQuantity { get; set; }

        public bool IsSuccess => Outcome == DungeonSettlementOutcome.Escaped;
    }
}
