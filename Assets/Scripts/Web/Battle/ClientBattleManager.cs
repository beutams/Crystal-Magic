using CrystalMagic.Core;
using CrystalMagic.Game.Unit;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;
using System.Linq;

namespace Server
{
    public class ClientBattleManager
    {
        public ClientService battleServic => ClientNetworkManager.Instance.clientServic;
        public ClientFrameManager frame;
        public Connect battleConnect;
        public string ticket;
        public Dictionary<Guid, Entity> networkEntities = new Dictionary<Guid, Entity>();
        private BattleEnterData battleData;

        private NetworkEntitySpawnInfo[] pendingEntityInfos;
        private bool sceneInitialized;
        private bool entitiesInitialized;

        public void Cleanup()
        {
            frame?.Stop();
            if (battleConnect != null)
            {
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2C_EnterBattleResult>(), OnEnterBattleResult);
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2C_EnterBattleScene>(), OnEnterBattleScene);
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2C_CreateNetworkEntities>(), OnCreateNetworkEntities);
                battleConnect.UnRegisterCallback(TCPPacketCode.GetOpcode<B2C_StartFrame>(), OnStartFrame);
                battleConnect.OnConnected -= OnBattleConnected;
                battleConnect.OnDisconnected -= OnBattleDisconnected;
                battleServic.Disconnect(battleConnect);
                battleConnect = null;
            }

            ticket = null;
            networkEntities.Clear();
            battleData = null;
            pendingEntityInfos = null;
            sceneInitialized = false;
            entitiesInitialized = false;
            frame = null;
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
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2C_EnterBattleScene>(), OnEnterBattleScene);
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2C_CreateNetworkEntities>(), OnCreateNetworkEntities);
            battleConnect.RegisterCallback(TCPPacketCode.GetOpcode<B2C_StartFrame>(), OnStartFrame);
        }
        private void OnBattleConnected(Connect connect)
        {
            CharacterData characterData = SaveDataComponent.Instance.GetCharacterData();
            if (characterData == null)
            {
                Debug.LogError("[Battle] Character data is unavailable.");
                battleServic.Disconnect(connect);
                return;
            }

            connect.Send(new C2B_EnterBattle()
            {
                ticket = ticket,
                data = characterData,
            });
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
                    frame = ClientFrameManager.Instance;
                    frame.AddConnect(connect);
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
            frame?.Stop();
            battleConnect = null;
            ticket = null;
            networkEntities.Clear();
            battleData = null;
            pendingEntityInfos = null;
            sceneInitialized = false;
            entitiesInitialized = false;
            frame = null;
        }

        private void OnStartFrame(IMessage message, Connect connect)
        {
            B2C_StartFrame realMessage = message as B2C_StartFrame;
            if (realMessage == null ||
                connect != battleConnect ||
                battleData == null ||
                realMessage.battleId != battleData.battleId ||
                !entitiesInitialized ||
                frame == null)
            {
                return;
            }

            frame.Start();
        }

        private void OnEnterBattleScene(IMessage message, Connect connect)
        {
            B2C_EnterBattleScene realMessage = message as B2C_EnterBattleScene;
            if (realMessage == null || realMessage.battleData == null || realMessage.battleData.battleId == 0)
            {
                return;
            }

            if (battleData != null)
            {
                if (battleData.battleId != realMessage.battleData.battleId)
                    Debug.LogError("[Battle] Received a scene message for another battle.");

                return;
            }

            battleData = realMessage.battleData;
            sceneInitialized = false;
            entitiesInitialized = false;
            GameWorldManager.CreateGameWorld(GameWorldRole.Client);
            GameFlowComponent.Instance.BeginTransition(OnlineBattlePreparationState.CreateEnterTransitionData(battleData));
        }

        public void OnBattleSceneInitialized()
        {
            if (battleData == null || sceneInitialized)
                return;

            sceneInitialized = true;
            TryInitializeNetworkEntities();
        }

        private void OnCreateNetworkEntities(IMessage message, Connect connect)
        {
            B2C_CreateNetworkEntities realMessage = message as B2C_CreateNetworkEntities;
            if (realMessage == null ||
                battleData == null ||
                realMessage.battleId != battleData.battleId ||
                entitiesInitialized)
            {
                return;
            }

            pendingEntityInfos = realMessage.entityInfos ?? Array.Empty<NetworkEntitySpawnInfo>();
            TryInitializeNetworkEntities();
        }

        private void TryInitializeNetworkEntities()
        {
            if (!sceneInitialized || pendingEntityInfos == null || entitiesInitialized || !GameWorldManager.TryGetEntityManager(out EntityManager entityManager))
                return;

            for (int index = 0; index < pendingEntityInfos.Length; index++)
            {
                NetworkEntitySpawnInfo entityInfo = pendingEntityInfos[index];
                if (entityInfo == null ||
                    entityInfo.unitId == Guid.Empty ||
                    string.IsNullOrEmpty(entityInfo.prefabName))
                {
                    Debug.LogError("[Battle] Received invalid network entity spawn data.");
                    return;
                }

                if (networkEntities.ContainsKey(entityInfo.unitId))
                    continue;

                Entity entity;
                FixedString128Bytes prefabName = new FixedString128Bytes(entityInfo.prefabName);
                switch (entityInfo.prefabType)
                {
                    case NetworkEntityPrefabType.Unit:
                        if (!EntitySpawnRegistryUtility.TryInstantiateUnit(entityManager, prefabName, out entity))
                        {
                            Debug.LogError($"[Battle] Failed to create unit '{entityInfo.prefabName}'.");
                            return;
                        }
                        break;
                    case NetworkEntityPrefabType.Environment:
                        if (!EntitySpawnRegistryUtility.TryInstantiateEnvironment(entityManager, prefabName, out entity))
                        {
                            Debug.LogError($"[Battle] Failed to create environment '{entityInfo.prefabName}'.");
                            return;
                        }
                        break;
                    case NetworkEntityPrefabType.Drop:
                        if (!EntitySpawnRegistryUtility.TryInstantiateDrop(entityManager, prefabName, out entity))
                        {
                            Debug.LogError($"[Battle] Failed to create drop '{entityInfo.prefabName}'.");
                            return;
                        }
                        break;
                    case NetworkEntityPrefabType.Projectile:
                        if (!EntitySpawnRegistryUtility.TryInstantiateProjectile(entityManager, prefabName, out entity))
                        {
                            Debug.LogError($"[Battle] Failed to create projectile '{entityInfo.prefabName}'.");
                            return;
                        }
                        break;
                    default:
                        Debug.LogError($"[Battle] Unsupported network entity type: {entityInfo.prefabType}.");
                        return;
                }

                if (entityManager.HasComponent<LocalTransform>(entity))
                {
                    LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entity);
                    transform.Position = new Unity.Mathematics.float3(entityInfo.x, entityInfo.y, entityInfo.z);
                    entityManager.SetComponentData(entity, transform);
                }

                if (entityManager.HasComponent<NetworkIdentityComponent>(entity))
                    entityManager.SetComponentData(entity, new NetworkIdentityComponent { id = entityInfo.unitId });
                else
                    entityManager.AddComponentData(entity, new NetworkIdentityComponent { id = entityInfo.unitId });

                if (entityInfo.characterData != null)
                {
                    if (entityManager.HasComponent<PlayerCharacterComponent>(entity))
                        entityManager.GetComponentObject<PlayerCharacterComponent>(entity).Data = entityInfo.characterData;
                    else
                        entityManager.AddComponentObject(entity, new PlayerCharacterComponent { Data = entityInfo.characterData });
                }

                if (entityManager.HasComponent<UnitVitalityComponent>(entity))
                {
                    UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
                    vitality.CurrentHealth = entityInfo.health;
                    entityManager.SetComponentData(entity, vitality);
                }

                if (entityManager.HasComponent<UnitManaComponent>(entity))
                {
                    UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(entity);
                    mana.CurrentMana = entityInfo.mana;
                    entityManager.SetComponentData(entity, mana);
                }

                if (entityInfo.ownerAccountId == ClientNetworkManager.Instance.clientLobbyManager.accountId)
                {
                    if (entityManager.HasComponent<NetworkPlayerComponent>(entity))
                        entityManager.SetComponentData(entity, new NetworkPlayerComponent { id = entityInfo.unitId });
                    else
                        entityManager.AddComponentData(entity, new NetworkPlayerComponent { id = entityInfo.unitId });
                }

                networkEntities.Add(entityInfo.unitId, entity);
            }

            entitiesInitialized = true;
            if (battleConnect != null)
                battleConnect.Send(new C2B_BattleReady { battleId = battleData.battleId });
        }
    }
}
