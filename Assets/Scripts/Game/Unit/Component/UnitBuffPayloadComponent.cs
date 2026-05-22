using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;

public class UnitBuffPayloadComponent : IComponentData
{
    public int NextPayloadId;
    public List<UnitBuffPayloadEntry> Entries = new();
}

public sealed class UnitBuffPayloadEntry
{
    public int PayloadId = -1;
    public bool HasOriginEntity;
    public Entity OriginEntity = Entity.Null;
    public EffectData[] RuntimeEffectChain = Array.Empty<EffectData>();
}
