using CrystalMagic.Game.Data;
using NUnit.Framework;
using Unity.Entities;

public sealed class GameInteractionDataTests
{
    [Test]
    public void DropData_RetainsItemAmountAndRewardType()
    {
        UnitInteractionData data = UnitInteractionData.CreateDrop(DropRewardType.Money, -1, 25);

        Assert.That(data.Kind, Is.EqualTo(InteractionKind.Drop));
        Assert.That(data.DataId, Is.EqualTo(-1));
        Assert.That(data.Amount, Is.EqualTo(25));
        Assert.That(data.Variant, Is.EqualTo((int)DropRewardType.Money));
    }

    [Test]
    public void InteractionDataComparison_UsesTheFullRequestSnapshot()
    {
        UnitInteractionData original = UnitInteractionData.CreateDrop(DropRewardType.Item, 101, 2);
        UnitInteractionData changedAmount = UnitInteractionData.CreateDrop(DropRewardType.Item, 101, 3);
        UnitInteractionData changedType = UnitInteractionData.CreateDrop(DropRewardType.Money, 101, 2);

        Assert.That(GameInteractionTargetUtility.IsSameData(original, original), Is.True);
        Assert.That(GameInteractionTargetUtility.IsSameData(original, changedAmount), Is.False);
        Assert.That(GameInteractionTargetUtility.IsSameData(original, changedType), Is.False);
    }

    [Test]
    public void RequestInteraction_DefaultsToCurrentCandidateGetter()
    {
        RequestInteractionActionNodeData node = new();

        Assert.That(node.Type, Is.EqualTo("RequestInteraction"));
        Assert.That(node.Interaction.Source, Is.EqualTo(InteractionRequestSource.Getter));
        Assert.That(node.Interaction.GetterKey, Is.EqualTo("game.interaction.candidate"));
    }

    [Test]
    public void Submit_RejectsRequestsWhileInteractionIsActive()
    {
        using World world = new("GameInteractionDataTests");
        EntityManager entityManager = world.EntityManager;
        Entity candidate = entityManager.CreateEntity();
        entityManager.AddComponentData(candidate, new InteractionCandidateComponent
        {
            IsInteracting = 1,
        });
        Entity requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, default(GameInteractionRequest));

        InteractionRequestSnapshot interaction = new()
        {
            Target = entityManager.CreateEntity(),
            Data = UnitInteractionData.CreateDrop(DropRewardType.Money, -1, 5),
        };

        Assert.That(GameInteractionRequestUtility.TrySubmit(entityManager, Entity.Null, interaction), Is.False);

        entityManager.SetComponentData(candidate, default(InteractionCandidateComponent));
        Assert.That(GameInteractionRequestUtility.TrySubmit(entityManager, Entity.Null, interaction), Is.True);
        Assert.That(entityManager.GetComponentData<GameInteractionRequest>(requestEntity).HasRequest, Is.EqualTo(1));
    }
}
