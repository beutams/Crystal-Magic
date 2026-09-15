using CrystalMagic.UI;
using UnityEngine;

namespace CrystalMagic.Core
{
    public sealed class DungeonSettlementStateData
    {
        public DungeonSettlementOutcome Outcome { get; set; }

        public static DungeonSettlementStateData Create(DungeonSettlementOutcome outcome)
        {
            return new DungeonSettlementStateData
            {
                Outcome = outcome,
            };
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

            DungeonSettlementStateData data = StateData as DungeonSettlementStateData;
            DungeonSettlementOutcome outcome = data?.Outcome ?? DungeonSettlementOutcome.Escaped;
            DungeonSettlementResult result = SaveDataComponent.Instance.SettleDungeonRun(outcome);

            _settlementUI = UIComponent.Instance.Open<DungeonSettlementUI>(new DungeonSettlementUIOpenData
            {
                Title = GetTitle(result),
                Summary = GetContent(result),
                ConfirmLabel = "返回城镇",
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

        private void ReturnToTown()
        {
            if (_isReturningToTown)
                return;

            _isReturningToTown = true;
            LoadGameContext context = SaveDataComponent.Instance.CreateLoadGameContext(SaveAreaType.Town);
            GameFlowComponent.Instance.BeginTransition(TownState.CreateEnterTransitionData(context));
        }

        private static string GetTitle(DungeonSettlementResult result)
        {
            return result != null && result.IsSuccess ? "成功离开地下城" : "地下城战败";
        }

        private static string GetContent(DungeonSettlementResult result)
        {
            if (result == null)
                return "本次地牢记录已结束。";

            return result.IsSuccess
                ? $"抵达层数：{result.ReachedFloor}\n角色当前携带的数据已带回城镇。"
                : $"抵达层数：{result.ReachedFloor}\n本次地牢角色数据已丢弃。";
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
