using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core
{
    public enum GameGateType
    {
        Simulation,
        PlayerInput,
        UIInput,
    }

    public class GameGateComponent : GameComponent<GameGateComponent>
    {
        private readonly Dictionary<GameGateType, HashSet<string>> _locks = new();
        private float _timeScaleBeforeSimulationLock = 1f;
        private bool _hasAppliedSimulationLock;

        public override int Priority => 4;

        public bool IsSimulationLocked => IsLocked(GameGateType.Simulation);
        public bool IsPlayerInputLocked => IsLocked(GameGateType.PlayerInput);
        public bool IsUIInputLocked => IsLocked(GameGateType.UIInput);

        public void Lock(GameGateType gateType, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                reason = "Unknown";

            if (!_locks.TryGetValue(gateType, out HashSet<string> reasons))
            {
                reasons = new HashSet<string>();
                _locks[gateType] = reasons;
            }

            bool wasLocked = reasons.Count > 0;
            reasons.Add(reason);

            if (gateType == GameGateType.Simulation && !wasLocked)
                ApplySimulationLock();
        }

        public void Unlock(GameGateType gateType, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                reason = "Unknown";

            if (!_locks.TryGetValue(gateType, out HashSet<string> reasons))
                return;

            reasons.Remove(reason);
            if (reasons.Count > 0)
                return;

            _locks.Remove(gateType);
            if (gateType == GameGateType.Simulation)
                ReleaseSimulationLock();
        }

        public bool IsLocked(GameGateType gateType)
        {
            return _locks.TryGetValue(gateType, out HashSet<string> reasons) && reasons.Count > 0;
        }

        public override void Cleanup()
        {
            _locks.Clear();
            ReleaseSimulationLock();
            base.Cleanup();
        }

        private void ApplySimulationLock()
        {
            if (_hasAppliedSimulationLock)
                return;

            _timeScaleBeforeSimulationLock = Time.timeScale;
            Time.timeScale = 0f;
            _hasAppliedSimulationLock = true;
        }

        private void ReleaseSimulationLock()
        {
            if (!_hasAppliedSimulationLock)
                return;

            Time.timeScale = _timeScaleBeforeSimulationLock;
            _hasAppliedSimulationLock = false;
        }
    }
}
