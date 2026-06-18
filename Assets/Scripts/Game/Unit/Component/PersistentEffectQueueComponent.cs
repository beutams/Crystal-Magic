using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using UnityEngine;

public sealed class PersistentEffectQueueComponent : IComponentData
{
    public List<PersistentEffectRequest> Requests = new();

    public void Enqueue(PersistentEffectRequest request)
    {
        if (request == null || request.Data == null || request.SourceContext == null)
            return;

        Requests.Add(request);
    }
}

public sealed class PersistentEffectRequest
{
    public PersistentEffectData Data;
    public SkillContent SourceContext;
    public Vector3 ReleasePosition;
}
