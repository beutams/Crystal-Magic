using System.Collections;
using Server;
using CrystalMagic.Game.Unit;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.Core
{
    public class OnlineBattlePreparationState : GameState
    {
        public const string SceneName = DungeonState.SceneName;
        private const int SpawnRegistryWaitFrames = 60;

        public static TransitionData CreateEnterTransitionData(BattleEnterData battleData)
        {
            return new TransitionData
            {
                TargetSceneName = SceneName,
                TargetStateType = typeof(OnlineBattlePreparationState),
                TargetStateData = battleData,
                TransitionUIName = "TransitionUI",
                KeepCurrentMainScene = true,
                ActiveSubSceneNames = new[] { DungeonState.RegistrySubSceneName },
                PostLoadCoroutineFactory = InitializeBattleScene,
            };
        }

        private static IEnumerator InitializeBattleScene()
        {
            for (int frame = 0; frame < SpawnRegistryWaitFrames; frame++)
            {
                World world = World.DefaultGameObjectInjectionWorld;
                if (world != null && world.IsCreated)
                {
                    EntityManager entityManager = world.EntityManager;
                    EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EntitySpawnRegistrySingleton>());
                    if (!query.IsEmptyIgnoreFilter)
                    {
                        ClientNetworkManager.Instance.clientBattleManager.OnBattleSceneInitialized();
                        yield break;
                    }
                }

                yield return null;
            }

            Debug.LogError("[OnlineBattlePreparationState] Entity spawn registry is unavailable in DungeonScene.");
        }

        public override void OnEnter()
        {
            InputComponent.Instance.SetBattleInputEnabled(false);
        }
    }
}
