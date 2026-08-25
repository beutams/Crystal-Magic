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

    protected override void OnUpdate()
    {
        foreach (UnitSkillReleaseComponent releaseComponent in
                 SystemAPI.Query<UnitSkillReleaseComponent>())
        {
            int requestCount = releaseComponent?.PendingRequests?.Count ?? 0;
            for (int i = 0; i < requestCount; i++)
            {
                SkillReleaseRequest request = releaseComponent.PendingRequests[0];
                releaseComponent.PendingRequests.RemoveAt(0);

                if (!SkillReleaseSnapshotUtility.TryCreate(EntityManager, request, out ResolvedSkillData resolvedSkill))
                {
                    Debug.LogError($"[SkillReleaseSystem] Failed to analyze SkillId={request?.SkillId ?? -1}.");
                    continue;
                }

                if (!SkillReleaseUtility.TryExecute(EntityManager, request, resolvedSkill, _context))
                    Debug.LogError($"[SkillReleaseSystem] Failed to execute SkillId={request.SkillId}.");
            }
        }
    }
}
