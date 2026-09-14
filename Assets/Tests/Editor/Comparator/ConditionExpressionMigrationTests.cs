using System.Collections.Generic;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using CrystalMagic.Game.Skill.Effects;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

public sealed class ConditionExpressionMigrationTests
{
    [Test]
    public void BuildComparator_EvaluatesTypedGetterAgainstLiteral()
    {
        ComparatorFactory factory = CreateFactory();
        TestResolver resolver = new();
        resolver.Add("test.value", UnitValue.FromInt(5));

        Comparator comparator = factory.BuildComparator(new List<ConditionConfig>
        {
            new()
            {
                CompareType = "GreaterThan",
                Inputs = new List<ValueExpression>
                {
                    Getter("test.value"),
                    Literal(UnitValue.FromInt(3)),
                },
            },
        }, resolver);

        Assert.That(comparator.IsValid, Is.True);
        Assert.That(comparator.GetResult(), Is.True);
    }

    [Test]
    public void BuildComparator_EvaluatesUnaryBooleanExpression()
    {
        ComparatorFactory factory = CreateFactory();
        TestResolver resolver = new();
        resolver.Add("test.enabled", UnitValue.FromBool(true));

        Comparator comparator = factory.BuildComparator(new List<ConditionConfig>
        {
            new()
            {
                CompareType = "IsTrue",
                Inputs = new List<ValueExpression> { Getter("test.enabled") },
            },
        }, resolver);

        Assert.That(comparator.IsValid, Is.True);
        Assert.That(comparator.GetResult(), Is.True);
    }

    [Test]
    public void BuildComparator_MissingInputsProducesInvalidComparator()
    {
        ComparatorFactory factory = CreateFactory();

        Comparator comparator = factory.BuildComparator(new List<ConditionConfig>
        {
            new()
            {
                CompareType = "Equal",
                Inputs = new List<ValueExpression> { Literal(UnitValue.FromBool(true)) },
            },
        }, new TestResolver());

        Assert.That(comparator.IsValid, Is.False);
        Assert.That(comparator.GetResult(), Is.False);
    }

    [Test]
    public void UnitValueJsonConverter_SerializesFloat2WithoutSwizzleLoop()
    {
        ValueExpression expression = Literal(UnitValue.FromFloat2(new float2(2f, 3f)));
        JsonSerializerSettings settings = new()
        {
            Converters = { new StateScriptUnitValueConverter() },
        };

        string json = JsonConvert.SerializeObject(expression, settings);
        ValueExpression roundTrip = JsonConvert.DeserializeObject<ValueExpression>(json, settings);

        Assert.That(json, Does.Not.Contain("xxxx"));
        Assert.That(roundTrip.Literal.TryGetFloat2(out float2 value), Is.True);
        Assert.That(value, Is.EqualTo(new float2(2f, 3f)));
    }

    private static ComparatorFactory CreateFactory()
    {
        ComparatorFactory factory = new();
        ComparatorRegistry.RegisterAll(factory);
        return factory;
    }

    private static ValueExpression Getter(string key) => new()
    {
        Kind = ValueExpressionKind.Getter,
        GetterKey = key,
    };

    private static ValueExpression Literal(UnitValue value) => new()
    {
        Kind = ValueExpressionKind.Literal,
        Literal = value,
    };

    private sealed class TestResolver : IComparatorValueResolver
    {
        private readonly Dictionary<string, IParameterizedUnitValueGetter> _getters = new();

        public void Add(string key, UnitValue value)
        {
            _getters.Add(key, new FixedGetter(value));
        }

        public bool TryGet(string key, out IParameterizedUnitValueGetter getter)
        {
            return _getters.TryGetValue(key, out getter);
        }
    }

    private sealed class FixedGetter : IParameterizedUnitValueGetter
    {
        private static readonly ComparatorParameterDefinition[] s_parameters = System.Array.Empty<ComparatorParameterDefinition>();
        private readonly UnitValue _value;

        public FixedGetter(UnitValue value)
        {
            _value = value;
        }

        public UnitValueCategory ReturnType => _value.Category;
        public IReadOnlyList<ComparatorParameterDefinition> Parameters => s_parameters;

        public bool TryGet(UnitValue[] parameters, out UnitValue value)
        {
            value = _value;
            return true;
        }
    }
}

public sealed class EffectConditionUtilityTests
{
    [Test]
    public void Pass_UsesCandidateFactionAndOriginContext()
    {
        using World world = new("EffectConditionUtilityTests");
        EntityManager entityManager = world.EntityManager;
        Entity origin = CreateUnit(entityManager, UnitFactionType.Player);
        Entity candidate = CreateUnit(entityManager, UnitFactionType.Enemy);
        SkillContent context = CreateContext(entityManager, origin);

        Assert.That(EffectConditionUtility.Pass(EnemyOfOriginCondition(), context, candidate), Is.True);

        entityManager.SetComponentData(candidate, new UnitFactionComponent { Value = UnitFactionType.Friend });
        Assert.That(EffectConditionUtility.Pass(EnemyOfOriginCondition(), context, candidate), Is.False);

        context.HasOriginEntity = false;
        Assert.That(EffectConditionUtility.Pass(EnemyOfOriginCondition(), context, candidate), Is.False);
    }

    [Test]
    public void Pass_ResolvesEffectContextValues()
    {
        using World world = new("EffectConditionUtilityContextTests");
        EntityManager entityManager = world.EntityManager;
        Entity origin = CreateUnit(entityManager, UnitFactionType.Player);
        Entity candidate = CreateUnit(entityManager, UnitFactionType.Enemy);
        Entity target = CreateUnit(entityManager, UnitFactionType.Enemy);
        Entity other = CreateUnit(entityManager, UnitFactionType.Friend);
        SkillContent context = CreateContext(entityManager, origin);
        context.HasTargetEntity = true;
        context.TargetEntity = target;
        context.HasOtherEntity = true;
        context.OtherEntity = other;
        context.HasPosition = true;
        context.Position = new UnityEngine.Vector3(2f, 3f, 4f);
        context.TriggerValue = 7f;

        Assert.That(EffectConditionUtility.Pass(EqualContextValue(EffectConditionUtility.TargetEntityKey, UnitValue.FromEntity(target)), context, candidate), Is.True);
        Assert.That(EffectConditionUtility.Pass(EqualContextValue(EffectConditionUtility.OtherEntityKey, UnitValue.FromEntity(other)), context, candidate), Is.True);
        Assert.That(EffectConditionUtility.Pass(EqualContextValue(EffectConditionUtility.PositionKey, UnitValue.FromFloat3(new float3(2f, 3f, 4f))), context, candidate), Is.True);
        Assert.That(EffectConditionUtility.Pass(EqualContextValue(EffectConditionUtility.TriggerValueKey, UnitValue.FromFloat(7f)), context, candidate), Is.True);

        context.HasOtherEntity = false;
        Assert.That(EffectConditionUtility.Pass(EqualContextValue(EffectConditionUtility.OtherEntityKey, UnitValue.FromEntity(other)), context, candidate), Is.False);
    }

    private static Entity CreateUnit(EntityManager entityManager, UnitFactionType faction)
    {
        Entity entity = entityManager.CreateEntity(typeof(UnitFactionComponent));
        entityManager.SetComponentData(entity, new UnitFactionComponent { Value = faction });

        UnitSourceAccessTable table = new();
        new UnitFactionSource().Bind(new UnitSourceBindingContext(entity, entityManager), table);
        entityManager.AddComponentObject(entity, new UnitSourceRuntimeComponent { Table = table });
        return entity;
    }

    private static SkillContent CreateContext(EntityManager entityManager, Entity origin)
    {
        return new SkillContent
        {
            EntityManager = entityManager,
            HasOriginEntity = true,
            OriginEntity = origin,
        };
    }

    private static List<ConditionConfig> EnemyOfOriginCondition()
    {
        return new List<ConditionConfig>
        {
            new()
            {
                CompareType = "IsTrue",
                Inputs = new List<ValueExpression>
                {
                    Getter("unit.faction.isEnemyTo", Getter(EffectConditionUtility.OriginEntityKey)),
                },
            },
        };
    }

    private static List<ConditionConfig> EqualContextValue(string key, UnitValue literal)
    {
        return new List<ConditionConfig>
        {
            new()
            {
                CompareType = "Equal",
                Inputs = new List<ValueExpression>
                {
                    Getter(key),
                    new ValueExpression
                    {
                        Kind = ValueExpressionKind.Literal,
                        Literal = literal,
                    },
                },
            },
        };
    }

    private static ValueExpression Getter(string key, params ValueExpression[] inputs)
    {
        return new ValueExpression
        {
            Kind = ValueExpressionKind.Getter,
            GetterKey = key,
            Inputs = new List<ValueExpression>(inputs),
        };
    }
}
