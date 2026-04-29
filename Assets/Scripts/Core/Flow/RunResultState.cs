using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 结算状态
    /// </summary>
    public class RunResultState : GameState
    {
        public override void OnEnter()
        {
            Debug.Log("[RunResultState] Entered RunResult");
        }

        public override void OnExit()
        {
            Debug.Log("[RunResultState] Exited RunResult");
        }

        public override void OnUpdate()
        {
        }
    }
}
