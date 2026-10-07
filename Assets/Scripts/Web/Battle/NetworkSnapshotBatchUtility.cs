using System;
using System.Collections.Generic;

public static class NetworkSnapshotBatchUtility
{
    public static void Coalesce(List<NetworkFrameData> frames)
    {
        var latest = new Dictionary<(Guid, Type), (List<NetworkStateData> states, int index)>();
        foreach (var frame in frames)
        {
            for (int index = 0; index < frame.datas.Count; index++)
            {
                NetworkStateData state = frame.datas[index];
                if (state == null)
                    continue;
                if (!IsSnapshot(state))
                {
                    // Events are ordering barriers, including spawn/despawn and edit replies.
                    latest.Clear();
                    continue;
                }
                var key = (state.unitId, state.GetType());
                if (latest.TryGetValue(key, out var previous))
                    previous.states[previous.index] = null;
                latest[key] = (frame.datas, index);
            }
        }
        foreach (var frame in frames)
            frame.datas.RemoveAll(state => state == null);
        frames.RemoveAll(frame => frame.datas.Count == 0);
    }

    private static bool IsSnapshot(NetworkStateData state) => state is
        NetworkMoveStateData or NetworkFacingStateData or NetworkAnimationStateData or
        NetworkVitalityStateData or NetworkManaStateData or NetworkBuffStateData or
        NetworkControlStateData or NetworkBattlePlayerStatusStateData or
        NetworkInteractableStateData or NetworkTreasureStateData or NetworkProjectileStateData or
        NetworkPlayerPropCooldownStateData or NetworkPlayerSkillChainStateData;
}
