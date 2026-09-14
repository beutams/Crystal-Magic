using CrystalMagic.Core;
using CrystalMagic.Game.Unit;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using System.Linq;

namespace Server
{
    public class ClientBattleManager
    {
        public ClientService battleServic => ClientNetworkManager.Instance.clientServic;
        public Connect battleConnect;
        public string ticket;
        public BattleEnterData battleData;
        public World battleWorld;
        public Entity localPlayer;
        public Dictionary<Guid, Entity> players = new Dictionary<Guid, Entity>();
        private Guid playerIdentity;
        public void Cleanup()
        {
            if (battleConnect != null)
            {
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2C_EnterBattleResult>(), OnEnterBattleResult);
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2C_CreateBattleUnit>(), OnCreateBattleUnit);
                battleConnect.OnConnected -= OnBattleConnected;
                battleConnect.OnDisconnected -= OnBattleDisconnected;
                battleServic.Disconnect(battleConnect);
                battleConnect = null;
            }

            ticket = null;
            battleData = null;
            localPlayer = Entity.Null;
            players.Clear();
        }
        public void ConnectWithTicket(string ticket)
        {
            if (battleConnect != null || string.IsNullOrEmpty(ticket))
            {
                return;
            }
            this.ticket = ticket;
            battleServic.Connect(ServerUtility.GetBattleIPEndPoint(), out battleConnect);
            battleConnect.OnConnected += OnBattleConnected;
            battleConnect.OnDisconnected += OnBattleDisconnected;
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2C_EnterBattleResult>(), OnEnterBattleResult);
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2C_CreateBattleUnit>(), OnCreateBattleUnit);
        }
        private void OnBattleConnected(Connect connect)
        {
            connect.Send(new C2B_EnterBattle() { ticket = ticket });
        }
        private void OnEnterBattleResult(IMessage message, Connect connect)
        {
            B2C_EnterBattleResult realMessage = message as B2C_EnterBattleResult;
            if (realMessage == null)
            {
                return;
            }

            switch (realMessage.type)
            {
                case BattleRequestType.EnterBattleSuccess:
                    ticket = null;
                    battleData = realMessage.battleData;
                    if (battleData == null)
                    {
                        battleServic.Disconnect(connect);
                        return;
                    }
                    GameFlowComponent.Instance.BeginTransition(OnlineBattlePreparationState.CreateEnterTransitionData(battleData));
                    break;
                case BattleRequestType.EnterBattleFail:
                    ticket = null;
                    battleServic.Disconnect(connect);
                    break;
            }
        }
        private void OnBattleDisconnected(Connect connect)
        {
            if (battleConnect != connect)
            {
                return;
            }
            battleConnect = null;
            ticket = null;
            battleData = null;
            localPlayer = Entity.Null;
            players.Clear();
        }
        public void OnBattleSceneLoaded()
        {
            if (battleData == null)
            {
                return;
            }

            CharacterData characterData = SaveDataComponent.Instance.GetCharacterData();
            battleConnect.Send(new C2B_SendUserData() { data = characterData });
        }
        private void OnCreateBattleUnit(IMessage message, Connect connect)
        {
            B2C_CreateBattleUnit realMessage = message as B2C_CreateBattleUnit;
            if (realMessage == null || realMessage.unitData == null || realMessage.unitData.unitId == Guid.Empty)
            {
                return;
            }

            BattleUnitData unitData = realMessage.unitData;
            if (players.ContainsKey(unitData.unitId))
            {
                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!EntitySpawnRegistryUtility.TryInstantiateUnit(entityManager, new FixedString128Bytes("PlayerDungeon"), out Entity entity))
            {
                Debug.LogError("[Battle] Failed to create PlayerDungeon.");
                return;
            }

            entityManager.AddComponentData(entity, new NetworkIdentityComponent() { id = unitData.unitId });
            if (unitData.accountId == ClientNetworkManager.Instance.clientLobbyManager.accountId)
            {
                entityManager.AddComponentData(entity, new NetworkPlayerComponent() { id = unitData.unitId });
                localPlayer = entity;
            }

            players.Add(unitData.unitId, entity);
        }

        public SortedDictionary<uint, Queue<NetworkStateData>> allcmds = new SortedDictionary<uint, Queue<NetworkStateData>>();
        public SortedDictionary<uint, Queue<NetworkStateData>> receivedOrder = new SortedDictionary<uint, Queue<NetworkStateData>>();
        public SortedDictionary<uint, Queue<NetworkStateData>> sendOrder = new SortedDictionary<uint, Queue<NetworkStateData>>();
        public SortedDictionary<uint, Queue<NetworkStateData>> inputOrder = new SortedDictionary<uint, Queue<NetworkStateData>>();
        public uint currentFrame; //当前模拟帧
        public uint currentArrivedFrame; //当前收到服务器的帧
        private void OnFixedUpdate()
        {
            if(receivedOrder.Count > 0)
            {
                Queue<NetworkStateData> frame = receivedOrder.First().Value;
                List<NetworkStateData> needRollback = new List<NetworkStateData>();
                foreach (var data in frame)
                {
                    if(data.unitId != playerIdentity)
                    {
                        NetworkEventDictionary.Instance.Handle(data);
                    }
                    else
                    {
                        if(!CheckConsistency(data) || !data.hasChecked)
                        {
                            needRollback.Add(data);
                        }
                    }
                }
                foreach(var data in needRollback)
                {
                    RollBack(data);
                    int count = (int)(currentFrame - data.frame - 1);
                    while(count >= 0)
                    {
                        count--;
                    }
                }
            }
        }
        private void FixedUpdateManual()
        {

        }
        private void RollBack(NetworkStateData data)
        {

        }
        private bool CheckConsistency(NetworkStateData data)
        {
            throw new NotImplementedException();
        }
    }
}
