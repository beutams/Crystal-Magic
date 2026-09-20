using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateBefore(typeof(SkillProjectileSpawnSystem))]
[UpdateBefore(typeof(PersistentEffectSystem))]
[UpdateBefore(typeof(EffectExecutionSystem))]
public partial class SkillReleaseSystem : SystemBase
{
    private readonly SkillContent _context = new();
    private readonly List<SkillReleaseRequest> _pendingRequests = new();

    protected override void OnUpdate()
    {
        _pendingRequests.Clear();
        foreach (DynamicBuffer<SkillReleaseRequest> requests in
                 SystemAPI.Query<DynamicBuffer<SkillReleaseRequest>>())
        {
            if (requests.IsEmpty)
                continue;

            for (int i = 0; i < requests.Length; i++)
                _pendingRequests.Add(requests[i]);
            requests.Clear();
        }

        for (int i = 0; i < _pendingRequests.Count; i++)
        {
            SkillReleaseRequest request = _pendingRequests[i];
            if (!SkillReleaseSnapshotUtility.TryCreate(EntityManager, in request, out ResolvedSkillData resolvedSkill))
            {
                Debug.LogError($"[SkillReleaseSystem] Failed to analyze SkillId={request.SkillId}.");
                continue;
            }

            if (!SkillReleaseUtility.TryExecute(EntityManager, in request, resolvedSkill, _context))
            {
                Debug.LogError($"[SkillReleaseSystem] Failed to execute SkillId={request.SkillId}.");
                continue;
            }
        }

        _pendingRequests.Clear();
    }
}
