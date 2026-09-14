using CrystalMagic.Core;
using System;
using System.Collections.Generic;

public class NetworkEventDictionary : SingletonNonMono<NetworkEventDictionary>
{
    public Dictionary<Type, NetworkStateData> events = new Dictionary<Type, NetworkStateData>();

    public void Handle(NetworkStateData data)
    {

    }
}