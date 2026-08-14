using Unity.Entities;
using UnityEngine.SceneManagement;

[UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
public partial class TestSceneUnitSystemGuard : SystemBase
{
    private const string TestScenePath = "Assets/Scenes/Test.unity";

    protected override void OnUpdate()
    {
        if (SceneManager.GetActiveScene().path != TestScenePath)
        {
            Enabled = false;
            return;
        }

        DisableGroup<UnitInitializationSystemGroup>();
        DisableGroup<UnitDecisionSystemGroup>();
        DisableGroup<UnitExecutionSystemGroup>();
        DisableGroup<UnitPostProcessSystemGroup>();
        Enabled = false;
    }

    private void DisableGroup<T>() where T : ComponentSystemGroup
    {
        T group = World.GetExistingSystemManaged<T>();
        if (group != null)
            group.Enabled = false;
    }
}
