using CrystalMagic.UI;
using UnityEngine;

namespace CrystalMagic.Core
{
    public sealed class DungeonSettlementStateData
    {
        public DungeonSettlementResult Result { get; private set; }
        public LoadGameContext ReturnContext { get; private set; }
        public bool IsSaved { get; private set; }

        private DungeonSettlementStateData() { }

        public static DungeonSettlementStateData Create(DungeonSettlementOutcome outcome)
        {
            // SetState exits DungeonState first, which destroys the player with the map.
            // Settle and retain the return data while that player's inventory still exists.
            DungeonSettlementResult result = SaveDataComponent.Instance.SettleDungeonRun(outcome);
            LoadGameContext returnContext = SaveDataComponent.Instance.CreateLoadGameContext(SaveAreaType.Town);
            returnContext.Character = PlayerCharacterUtility.Clone(returnContext.Character);
            DungeonSettlementStateData data = new()
            {
                Result = result,
                ReturnContext = returnContext,
            };
            // Commit both outcomes before the report opens or DungeonState destroys the player.
            data.TrySave();
            return data;
        }

        public bool TrySave()
        {
            if (!IsSaved)
                IsSaved = SaveDataComponent.Instance.SaveDungeonSettlement(ReturnContext);
            return IsSaved;
        }
    }

    public sealed class DungeonSettlementState : GameState
    {
        private const string SimulationLockReason = "DungeonSettlementState.Simulation";
        private const string PlayerInputLockReason = "DungeonSettlementState.PlayerInput";

        private DungeonSettlementUI _settlementUI;
        private bool _isReturningToTown;

        public override void OnEnter()
        {
            _isReturningToTown = false;
            LockDungeon();

            DungeonSettlementStateData data = (DungeonSettlementStateData)StateData;

            _settlementUI = UIComponent.Instance.Open<DungeonSettlementUI>(new DungeonSettlementUIOpenData
            {
                Result = data.Result,
                SaveFailed = !data.IsSaved,
                ConfirmAction = ReturnToTown,
            });

            if (_settlementUI == null)
                ReturnToTown();
        }

        public override void OnExit()
        {
            if (_settlementUI != null && UIComponent.Instance.IsManaged(_settlementUI))
                UIComponent.Instance.CloseUI(_settlementUI);

            _settlementUI = null;
            UnlockDungeon();
        }

        private bool ReturnToTown()
        {
            if (_isReturningToTown)
                return false;

            DungeonSettlementStateData data = (DungeonSettlementStateData)StateData;
            if (!data.TrySave())
                return false;

            _isReturningToTown = true;
            GameFlowComponent.Instance.BeginTransition(TownState.CreateEnterTransitionData(data.ReturnContext));
            return true;
        }

        private static void LockDungeon()
        {
            GameGateComponent.Instance.Lock(GameGateType.Simulation, SimulationLockReason);
            GameGateComponent.Instance.Lock(GameGateType.PlayerInput, PlayerInputLockReason);
        }

        private static void UnlockDungeon()
        {
            GameGateComponent.Instance.Unlock(GameGateType.PlayerInput, PlayerInputLockReason);
            GameGateComponent.Instance.Unlock(GameGateType.Simulation, SimulationLockReason);
        }
    }
}
