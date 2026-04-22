using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    partial class PersistentEffectSystem : SystemBase
    {
        private readonly List<PersistentEffectInstance> _instances = new();
        private readonly List<PersistentEffectInstance> _pendingInstances = new();
        private bool _isUpdating;

        public static PersistentEffectSystem Default =>
            World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<PersistentEffectSystem>();

        public EntityManager EffectEntityManager => EntityManager;

        public void AddEffect(PersistentEffectData data, SkillContent sourceContext, Vector3 releasePosition)
        {
            SkillContent context = sourceContext.Clone();
            context.EntityManager = EntityManager;
            context.HasPosition = true;
            context.Position = releasePosition;
            context.HasTargetEntity = false;
            context.TargetEntity = Entity.Null;

            ExecuteEffects(data.OnStartEffects, context);

            if (data.TotalDuration <= 0f || data.TickIntervalSeconds <= 0f || data.OnTickEffects == null || data.OnTickEffects.Length == 0)
                return;

            PersistentEffectInstance instance = new()
            {
                TotalDuration = data.TotalDuration,
                TickIntervalSeconds = data.TickIntervalSeconds,
                NextTickTime = data.TickIntervalSeconds,
                Context = context,
                OnTickEffects = data.OnTickEffects,
            };

            if (_isUpdating)
                _pendingInstances.Add(instance);
            else
                _instances.Add(instance);
        }

        protected override void OnUpdate()
        {
            AppendPendingInstances();

            float deltaTime = SystemAPI.Time.DeltaTime;
            _isUpdating = true;
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                PersistentEffectInstance instance = _instances[i];
                instance.Elapsed += deltaTime;

                while (instance.NextTickTime <= instance.Elapsed &&
                       instance.NextTickTime <= instance.TotalDuration)
                {
                    SkillContent tickContext = instance.Context.Clone();
                    tickContext.EntityManager = EntityManager;
                    ExecuteEffects(instance.OnTickEffects, tickContext);
                    instance.NextTickTime += instance.TickIntervalSeconds;
                }

                if (instance.Elapsed >= instance.TotalDuration)
                    _instances.RemoveAt(i);
            }
            _isUpdating = false;

            AppendPendingInstances();
        }

        private void ExecuteEffects(EffectData[] effects, SkillContent context)
        {
            SkillExecutor.ExecuteEffects(effects, context);
        }

        private void AppendPendingInstances()
        {
            if (_pendingInstances.Count == 0)
                return;

            _instances.AddRange(_pendingInstances);
            _pendingInstances.Clear();
        }

        private sealed class PersistentEffectInstance
        {
            public float TotalDuration;
            public float TickIntervalSeconds;
            public float Elapsed;
            public float NextTickTime;
            public SkillContent Context;
            public EffectData[] OnTickEffects;
        }
    }
}
