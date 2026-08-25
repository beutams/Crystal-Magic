using System;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;

namespace CrystalMagic.Game.Skill
{
    public enum SkillAdditionActionStatus
    {
        Stopped,
        Running,
        Completed,
        Failed,
    }

    public readonly struct SkillAdditionActionBuildRequest
    {
        public SkillAdditionActionBuildRequest(SkillAdditionActionData data, SkillAdditionActionContext context)
        {
            Data = data;
            Context = context;
        }

        public SkillAdditionActionData Data { get; }
        public SkillAdditionActionContext Context { get; }
    }

    public sealed class SkillAdditionActionContext
    {
        public SkillAdditionActionContext(StateScriptRuntime runtime, string eventName, int additionId)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            EventName = eventName ?? string.Empty;
            AdditionId = additionId;
        }

        public StateScriptRuntime Runtime { get; }
        public EntityManager EntityManager => Runtime.EntityManager;
        public Entity Entity => Runtime.Entity;
        public UnitSourceAccessTable Sources => Runtime.Sources;
        public string EventName { get; }
        public int AdditionId { get; }
    }

    public abstract class SkillAdditionAction
    {
        protected SkillAdditionAction(SkillAdditionActionData data, SkillAdditionActionContext context)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        protected SkillAdditionActionData Data { get; }
        protected SkillAdditionActionContext Context { get; }
        public SkillAdditionActionStatus Status { get; private set; } = SkillAdditionActionStatus.Stopped;

        public void Start()
        {
            if (Status != SkillAdditionActionStatus.Stopped)
                return;

            Status = OnStart();
        }

        public void Tick()
        {
            if (Status != SkillAdditionActionStatus.Running)
                return;

            Status = OnTick();
        }

        public void Stop()
        {
            if (Status != SkillAdditionActionStatus.Running)
                return;

            OnStop();
            Status = SkillAdditionActionStatus.Stopped;
        }

        protected abstract SkillAdditionActionStatus OnStart();

        protected virtual SkillAdditionActionStatus OnTick() => SkillAdditionActionStatus.Completed;

        protected virtual void OnStop()
        {
        }
    }

    public sealed class SkillAdditionActionFactory : GeneratedFactory<Type, SkillAdditionActionBuildRequest, SkillAdditionAction>
    {
        public SkillAdditionAction CreateAction(SkillAdditionActionData data, SkillAdditionActionContext context)
        {
            return data == null ? null : Create(data.GetType(), new SkillAdditionActionBuildRequest(data, context));
        }
    }

    [FactoryKey("ModifyCurrentSkill", 0, "Modify Current Skill")]
    public sealed class ModifyCurrentSkillAdditionAction : SkillAdditionAction
    {
        private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

        private readonly ModifyCurrentSkillAdditionActionData _data;

        public ModifyCurrentSkillAdditionAction(ModifyCurrentSkillAdditionActionData data, SkillAdditionActionContext context)
            : base(data, context)
        {
            _data = data;
        }

        protected override SkillAdditionActionStatus OnStart()
        {
            if (_data.Modifiers == null)
                return SkillAdditionActionStatus.Completed;

            for (int i = 0; i < _data.Modifiers.Count; i++)
            {
                SkillAdditionModifierExpressionData modifier = _data.Modifiers[i];
                if (modifier == null ||
                    !Enum.IsDefined(typeof(SkillModifierChannel), modifier.Channel) ||
                    SkillModifierChannelUtility.IsInternalChannel(modifier.Channel) ||
                    !TryEvaluateNumber(modifier.Factor, out float factor) ||
                    !TryEvaluateNumber(modifier.Bonus, out float bonus) ||
                    !PlayerCurrentSkillUtility.AddPendingExtraModifier(
                        Context.EntityManager,
                        Context.Entity,
                        new SkillModifierEntry
                        {
                            Channel = modifier.Channel,
                            Factor = factor,
                            Bonus = bonus,
                        }))
                {
                    return SkillAdditionActionStatus.Failed;
                }
            }

            return SkillAdditionActionStatus.Completed;
        }

        private bool TryEvaluateNumber(ValueExpression expression, out float value)
        {
            value = 0f;
            if (!s_expressionFactory.TryBuildValueExpression(
                    expression ?? new ValueExpression { Literal = UnitValue.FromFloat(0f) },
                    Context.Sources,
                    out UnitValueCategory category,
                    out Func<UnitValue> getter,
                    out _) ||
                category != UnitValueCategory.Number ||
                !getter().TryGetNumber(out value) ||
                float.IsNaN(value) ||
                float.IsInfinity(value))
            {
                return false;
            }

            return true;
        }

        private static ComparatorFactory CreateExpressionFactory()
        {
            ComparatorFactory factory = new();
            ComparatorRegistry.RegisterAll(factory);
            return factory;
        }
    }

    [FactoryKey("SetSourceValue", 10, "Set Source Value")]
    public sealed class SetSourceValueSkillAdditionAction : SkillAdditionAction
    {
        private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

        private readonly SetSourceValueSkillAdditionActionData _data;

        public SetSourceValueSkillAdditionAction(SetSourceValueSkillAdditionActionData data, SkillAdditionActionContext context)
            : base(data, context)
        {
            _data = data;
        }

        protected override SkillAdditionActionStatus OnStart()
        {
            if (string.IsNullOrWhiteSpace(_data.SetterKey) ||
                !Context.Sources.TryGetDefinition(_data.SetterKey, out UnitSourceSet setter) ||
                (setter.RequiresKey && string.IsNullOrWhiteSpace(_data.Key)) ||
                _data.Values == null ||
                _data.Values.Count != setter.Parameters.Count)
            {
                return SkillAdditionActionStatus.Failed;
            }

            UnitValue[] values = new UnitValue[setter.Parameters.Count];
            for (int i = 0; i < values.Length; i++)
            {
                if (!s_expressionFactory.TryBuildValueExpression(
                        _data.Values[i] ?? new ValueExpression(),
                        Context.Sources,
                        out UnitValueCategory category,
                        out Func<UnitValue> getter,
                        out _) ||
                    !setter.Parameters[i].Accepts(category))
                {
                    return SkillAdditionActionStatus.Failed;
                }

                values[i] = getter();
            }

            bool didSet = setter.RequiresKey
                ? setter.TrySet(_data.Key, values[0])
                : setter.TrySet(values);
            return didSet ? SkillAdditionActionStatus.Completed : SkillAdditionActionStatus.Failed;
        }

        private static ComparatorFactory CreateExpressionFactory()
        {
            ComparatorFactory factory = new();
            ComparatorRegistry.RegisterAll(factory);
            return factory;
        }
    }

    [FactoryKey("ExecuteEffects", 20, "Execute Effects")]
    public sealed class ExecuteEffectsSkillAdditionAction : SkillAdditionAction
    {
        private readonly ExecuteEffectsSkillAdditionActionData _data;

        public ExecuteEffectsSkillAdditionAction(ExecuteEffectsSkillAdditionActionData data, SkillAdditionActionContext context)
            : base(data, context)
        {
            _data = data;
        }

        protected override SkillAdditionActionStatus OnStart()
        {
            int sourceSkillId = PlayerCurrentSkillUtility.TryGetCurrentSkillId(Context.EntityManager, Context.Entity, out int currentSkillId)
                ? currentSkillId
                : -1;
            SkillExecutor.ExecuteEffects(_data.Effects, new SkillContent
            {
                EntityManager = Context.EntityManager,
                TriggerSource = SkillTriggerSource.Script,
                HasOriginEntity = true,
                OriginEntity = Context.Entity,
                SourceSkillId = sourceSkillId,
                HasTargetEntity = true,
                TargetEntity = Context.Entity,
            });
            return SkillAdditionActionStatus.Completed;
        }
    }

    [FactoryKey("ReplayCurrentSkill", 30, "Replay Current Skill")]
    public sealed class ReplayCurrentSkillAdditionAction : SkillAdditionAction
    {
        public ReplayCurrentSkillAdditionAction(ReplayCurrentSkillAdditionActionData data, SkillAdditionActionContext context)
            : base(data, context)
        {
        }

        protected override SkillAdditionActionStatus OnStart()
        {
            if (!PlayerCurrentSkillUtility.TryGetCurrentSkillId(Context.EntityManager, Context.Entity, out int skillId) ||
                !Context.EntityManager.HasComponent<UnitSkillReleaseComponent>(Context.Entity))
            {
                return SkillAdditionActionStatus.Failed;
            }

            UnitSkillReleaseComponent releaseComponent = Context.EntityManager.GetComponentObject<UnitSkillReleaseComponent>(Context.Entity);
            if (releaseComponent == null)
                return SkillAdditionActionStatus.Failed;

            releaseComponent.PendingRequests.Add(SkillReleaseRequestUtility.Create(
                Context.EntityManager,
                Context.Entity,
                skillId,
                new SkillModifierSet()));
            return SkillAdditionActionStatus.Completed;
        }
    }
}
