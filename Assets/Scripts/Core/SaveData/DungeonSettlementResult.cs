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

        public bool IsSuccess => Outcome == DungeonSettlementOutcome.Escaped;
    }
}
