using CrystalMagic.Core;
using System;
using System.Collections.Generic;

public class NetworkEventDictionary : SingletonNonMono<NetworkEventDictionary>
{
    public Dictionary<Type, NetworkState> events = new Dictionary<Type, NetworkState>();

    public void Handle(NetworkState data)
    {

    }
}