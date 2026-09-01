using CrystalMagic.Core;
using UnityEngine;

namespace Server
{
    [DefaultExecutionOrder(-1000)]
    public class ClientNetworkManager : Singleton<ClientNetworkManager>
    {
        public ClientService clientServic;

        protected override void Awake()
        {
            base.Awake();
            clientServic = new ClientService();
            clientServic.Init();
        }

        private void LateUpdate()
        {
            clientServic.Update();
        }
    }
}
