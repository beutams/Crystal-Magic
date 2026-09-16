using System;
using System.Collections.Generic;

public struct NetworkState
{
    public bool hasChecked;
    public NetworkStateData data;
}
[Serializable]
public abstract class NetworkStateData
{
    public Guid unitId;

    public abstract void Apply(NetworkStateApplyContext context);
}
[Serializable]
public class NetworkFrameData
{
    public uint frameId;
    public List<NetworkStateData> datas;
}
