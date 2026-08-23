using CrystalMagic.Game.Data;
using Unity.Entities;

[FactoryKey("RequestSkillWithAddition", 13, "Request Skill With Addition")]
public sealed class RequestSkillWithAdditionActionNode : StateScriptActionNode
{
    private readonly StateScriptOutputPort _output;

    public RequestSkillWithAdditionActionNode(RequestSkillWithAdditionActionNodeData data, StateScriptRuntime runtime)
        : base(data, runtime)
    {
        AddInput("In", RequestSkillWithAddition);
        _output = AddOutput("Out");
    }

    protected override bool OnBind(out string error)
    {
        if (!Runtime.EntityManager.HasComponent<UnitSkillReleaseComponent>(Runtime.Entity))
        {
            error = "RequestSkillWithAddition requires UnitSkillReleaseComponent.";
            return false;
        }

        if (!Runtime.EntityManager.HasComponent<PlayerCurrentSkillComponent>(Runtime.Entity))
        {
            error = "RequestSkillWithAddition requires PlayerCurrentSkillComponent.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void RequestSkillWithAddition()
    {
        EntityManager entityManager = Runtime.EntityManager;
        Entity entity = Runtime.Entity;
        if (!PlayerCurrentSkillUtility.TryGetCurrentSkillId(entityManager, entity, out int skillId) ||
            !entityManager.HasComponent<UnitSkillReleaseComponent>(entity))
        {
            return;
        }

        UnitSkillReleaseComponent releaseComponent = entityManager.GetComponentObject<UnitSkillReleaseComponent>(entity);
        if (releaseComponent == null)
            return;

        SkillReleaseRequest request = SkillReleaseRequestUtility.Create(
            entityManager,
            entity,
            skillId,
            PlayerCurrentSkillUtility.ConsumePendingExtraModifiers(entityManager, entity));
        releaseComponent.PendingRequests.Add(request);
        _output.Pulse();
    }
}
