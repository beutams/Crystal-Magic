using System.Collections.Generic;

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
        public IReadOnlyList<InventoryItemData> Items { get; set; } = System.Array.Empty<InventoryItemData>();
        public long Money { get; set; }
        public bool HasCompleteHistory { get; set; } = true;

        public bool IsSuccess => Outcome == DungeonSettlementOutcome.Escaped;
    }
}
