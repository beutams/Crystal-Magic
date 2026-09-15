using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using UnityEngine;

[RunInGameWorld(GameWorldKind.Dungeon)]
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
        foreach (UnitSkillReleaseComponent releaseComponent in
                 SystemAPI.Query<UnitSkillReleaseComponent>())
        {
            if (releaseComponent?.PendingRequests == null || releaseComponent.PendingRequests.Count == 0)
                continue;

            _pendingRequests.AddRange(releaseComponent.PendingRequests);
            releaseComponent.PendingRequests.Clear();
        }

        for (int i = 0; i < _pendingRequests.Count; i++)
        {
            SkillReleaseRequest request = _pendingRequests[i];
            if (!SkillReleaseSnapshotUtility.TryCreate(EntityManager, request, out ResolvedSkillData resolvedSkill))
            {
                Debug.LogError($"[SkillReleaseSystem] Failed to analyze SkillId={request?.SkillId ?? -1}.");
                continue;
            }

            if (!SkillReleaseUtility.TryExecute(EntityManager, request, resolvedSkill, _context))
            {
                Debug.LogError($"[SkillReleaseSystem] Failed to execute SkillId={request.SkillId}.");
                continue;
            }

            UnitBuffHookUtility.Dispatch(
                EntityManager,
                request.OriginEntity,
                SkillHookType.OnCastComplete,
                SkillTriggerSource.ActiveCast,
                hasOriginEntity: true,
                originEntity: request.OriginEntity,
                sourceSkillId: request.SkillId,
                hasOtherEntity: request.HasTargetEntity,
                otherEntity: request.TargetEntity,
                hasPosition: request.HasTargetPosition,
                position: new Vector3(
                    request.TargetPosition.x,
                    request.TargetPosition.y,
                    request.TargetPosition.z));
        }

        _pendingRequests.Clear();
    }
}
