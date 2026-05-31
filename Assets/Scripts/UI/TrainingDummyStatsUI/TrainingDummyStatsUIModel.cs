using System.Collections.Generic;
using System.Text;
using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class TrainingDummyStatsUIModel : UIModelBase
    {
        public const string DataChangedEventName = "TrainingDummyStatsUIModel.DataChanged";

        private const string PreferredDummyUnitName = "MonsterStraw";
        private const float DpsWindowSeconds = 5f;

        private readonly List<DamageSample> _damageSamples = new();
        private Entity _cachedDummyEntity = Entity.Null;
        private World _cachedQueryWorld;
        private EntityQuery _dummyQuery;
        private string _displayText = string.Empty;
        private float _lastKnownHealth = -1f;
        private float _totalDamage;
        private float _combatStartTime = -1f;
        private bool _hasHealthSnapshot;

        public override string ChangedEventName => DataChangedEventName;
        public string DisplayText => _displayText;

        public void RefreshRuntime()
        {
            float now = Time.time;
            TrimExpiredDamage(now);

            TrainingDummySnapshot snapshot = BuildSnapshot();
            if (snapshot.Exists)
            {
                _lastKnownHealth = snapshot.CurrentHealth;
                _hasHealthSnapshot = true;
            }
            else
            {
                _cachedDummyEntity = Entity.Null;
                _lastKnownHealth = -1f;
                _hasHealthSnapshot = false;
            }

            string nextText = BuildDisplayText(snapshot, now);
            if (string.Equals(_displayText, nextText, System.StringComparison.Ordinal))
                return;

            _displayText = nextText;
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }

        public void HandleUnitDamaged(UnitDamagedEvent gameEvent)
        {
            if (!TryGetDummyEntity(out EntityManager entityManager, out Entity dummyEntity))
                return;

            if (gameEvent.TargetEntity != dummyEntity)
                return;

            float damage = 0f;
            if (_hasHealthSnapshot)
            {
                damage = Mathf.Max(0f, _lastKnownHealth - gameEvent.CurrentHealth);
            }
            else if (entityManager.Exists(dummyEntity) && entityManager.HasComponent<UnitVitalityComponent>(dummyEntity))
            {
                damage = Mathf.Max(0f, entityManager.GetComponentData<UnitVitalityComponent>(dummyEntity).CurrentHealth - gameEvent.CurrentHealth);
            }

            _lastKnownHealth = gameEvent.CurrentHealth;
            _hasHealthSnapshot = true;

            if (damage <= 0f)
                return;

            float now = Time.time;
            if (_combatStartTime < 0f)
                _combatStartTime = now;

            _damageSamples.Add(new DamageSample
            {
                Time = now,
                Amount = damage,
            });
            _totalDamage += damage;
            TrimExpiredDamage(now);
        }

        public override void Dispose()
        {
            ReleaseDummyQuery();
            _damageSamples.Clear();
            _cachedDummyEntity = Entity.Null;
            base.Dispose();
        }

        private TrainingDummySnapshot BuildSnapshot()
        {
            if (!TryGetDummyEntity(out EntityManager entityManager, out Entity dummyEntity))
                return TrainingDummySnapshot.Missing;

            TrainingDummySnapshot snapshot = new()
            {
                Exists = true,
                UnitName = PreferredDummyUnitName,
            };

            if (entityManager.HasComponent<UnitStateMachineComponent>(dummyEntity))
                snapshot.UnitName = entityManager.GetComponentObject<UnitStateMachineComponent>(dummyEntity)?.UnitName ?? PreferredDummyUnitName;

            if (entityManager.HasComponent<UnitVitalityComponent>(dummyEntity))
            {
                UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(dummyEntity);
                snapshot.CurrentHealth = vitality.CurrentHealth;
                snapshot.MaxHealth = vitality.RealMaxHealth;
                snapshot.HealthRegen = vitality.RealHealthRegenPerSecond;
                snapshot.Defense = vitality.RealDefense;
            }

            if (entityManager.HasComponent<UnitManaComponent>(dummyEntity))
            {
                UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(dummyEntity);
                snapshot.HasMana = true;
                snapshot.CurrentMana = mana.CurrentMana;
                snapshot.MaxMana = mana.RealMaxMp;
                snapshot.ManaRegen = mana.RealMpRegenPerSecond;
            }

            if (entityManager.HasComponent<UnitAttackComponent>(dummyEntity))
            {
                UnitAttackComponent attack = entityManager.GetComponentData<UnitAttackComponent>(dummyEntity);
                snapshot.AttackPower = attack.RealAttackPower;
                snapshot.SkillRange = attack.RealSkillRange;
                snapshot.ActionSpeedBonus = attack.RealActionSpeedBonus;
                snapshot.ChantSpeedBonus = attack.RealChantSpeedBonus;
            }

            if (entityManager.HasComponent<UnitMoveComponent>(dummyEntity))
            {
                UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(dummyEntity);
                snapshot.MoveSpeed = move.RealMoveSpeed;
                snapshot.MaxAcceleration = move.RealMaxAcceleration;
            }

            if (entityManager.HasBuffer<UnitBuffElement>(dummyEntity))
            {
                DynamicBuffer<UnitBuffElement> buffs = entityManager.GetBuffer<UnitBuffElement>(dummyEntity);
                int activeBuffCount = 0;
                for (int i = 0; i < buffs.Length; i++)
                {
                    if (buffs[i].BuffId >= 0 && buffs[i].StackCount > 0)
                        activeBuffCount++;
                }

                snapshot.ActiveBuffCount = activeBuffCount;
            }

            return snapshot;
        }

        private bool TryGetDummyEntity(out EntityManager entityManager, out Entity dummyEntity)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                dummyEntity = Entity.Null;
                return false;
            }

            entityManager = world.EntityManager;
            if (_cachedDummyEntity != Entity.Null &&
                entityManager.Exists(_cachedDummyEntity) &&
                entityManager.HasComponent<UnitFactionComponent>(_cachedDummyEntity) &&
                entityManager.HasComponent<UnitStateMachineComponent>(_cachedDummyEntity))
            {
                UnitStateMachineComponent cachedStateMachine = entityManager.GetComponentObject<UnitStateMachineComponent>(_cachedDummyEntity);
                if (cachedStateMachine != null &&
                    entityManager.GetComponentData<UnitFactionComponent>(_cachedDummyEntity).Value == UnitFactionType.Enemy &&
                    (cachedStateMachine.UnitName == PreferredDummyUnitName || !string.IsNullOrWhiteSpace(cachedStateMachine.UnitName)))
                {
                    dummyEntity = _cachedDummyEntity;
                    return true;
                }
            }

            if (!EnsureDummyQuery(world))
            {
                dummyEntity = Entity.Null;
                return false;
            }

            Entity fallbackEnemy = Entity.Null;
            using NativeArray<Entity> entities = _dummyQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!entityManager.HasComponent<UnitStateMachineComponent>(entity))
                    continue;

                if (entityManager.GetComponentData<UnitFactionComponent>(entity).Value != UnitFactionType.Enemy)
                    continue;

                UnitStateMachineComponent stateMachine = entityManager.GetComponentObject<UnitStateMachineComponent>(entity);
                if (stateMachine == null)
                    continue;

                if (fallbackEnemy == Entity.Null)
                    fallbackEnemy = entity;

                if (stateMachine.UnitName != PreferredDummyUnitName)
                    continue;

                _cachedDummyEntity = entity;
                dummyEntity = entity;
                return true;
            }

            if (fallbackEnemy != Entity.Null)
            {
                _cachedDummyEntity = fallbackEnemy;
                dummyEntity = fallbackEnemy;
                return true;
            }

            dummyEntity = Entity.Null;
            return false;
        }

        private bool EnsureDummyQuery(World world)
        {
            if (world == null || !world.IsCreated)
                return false;

            if (_cachedQueryWorld == world)
                return true;

            ReleaseDummyQuery();
            _cachedQueryWorld = world;
            _dummyQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitFactionComponent>(),
                ComponentType.ReadOnly<UnitVitalityComponent>());
            return true;
        }

        private void ReleaseDummyQuery()
        {
            if (_cachedQueryWorld != null && _cachedQueryWorld.IsCreated)
                _dummyQuery.Dispose();

            _cachedQueryWorld = null;
            _dummyQuery = default;
        }

        private void TrimExpiredDamage(float now)
        {
            for (int i = _damageSamples.Count - 1; i >= 0; i--)
            {
                if (now - _damageSamples[i].Time <= DpsWindowSeconds)
                    continue;

                _damageSamples.RemoveAt(i);
            }
        }

        private string BuildDisplayText(TrainingDummySnapshot snapshot, float now)
        {
            if (!snapshot.Exists)
                return "训练场数据\n未找到训练目标";

            float windowDamage = 0f;
            for (int i = 0; i < _damageSamples.Count; i++)
                windowDamage += _damageSamples[i].Amount;

            float windowDps = windowDamage / DpsWindowSeconds;
            float averageDps = 0f;
            if (_combatStartTime >= 0f)
            {
                float combatDuration = Mathf.Max(0.001f, now - _combatStartTime);
                averageDps = _totalDamage / combatDuration;
            }

            StringBuilder builder = new(512);
            builder.AppendLine("训练场数据");
            builder.Append("目标: ").AppendLine(snapshot.UnitName);
            builder.Append("生命: ").Append(Format(snapshot.CurrentHealth)).Append(" / ").AppendLine(Format(snapshot.MaxHealth));
            builder.Append("法力: ");
            if (snapshot.HasMana)
                builder.Append(Format(snapshot.CurrentMana)).Append(" / ").AppendLine(Format(snapshot.MaxMana));
            else
                builder.AppendLine("无");

            builder.Append("攻击: ").AppendLine(Format(snapshot.AttackPower));
            builder.Append("防御: ").AppendLine(Format(snapshot.Defense));
            builder.Append("移速: ").AppendLine(Format(snapshot.MoveSpeed));
            builder.Append("加速度: ").AppendLine(Format(snapshot.MaxAcceleration));
            builder.Append("技能范围: ").AppendLine(Format(snapshot.SkillRange));
            builder.Append("行动速度加成: ").Append(Format(snapshot.ActionSpeedBonus)).AppendLine("%");
            builder.Append("咏唱速度加成: ").Append(Format(snapshot.ChantSpeedBonus)).AppendLine("%");
            builder.Append("生命回复: ").AppendLine(Format(snapshot.HealthRegen));
            builder.Append("法力回复: ").AppendLine(Format(snapshot.ManaRegen));
            builder.Append("当前 Buff 数: ").AppendLine(snapshot.ActiveBuffCount.ToString());
            builder.Append("累计伤害: ").AppendLine(Format(_totalDamage));
            builder.Append("近5秒 DPS: ").AppendLine(Format(windowDps));
            builder.Append("平均 DPS: ").AppendLine(Format(averageDps));
            return builder.ToString();
        }

        private static string Format(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }

        private struct DamageSample
        {
            public float Time;
            public float Amount;
        }

        private struct TrainingDummySnapshot
        {
            public static TrainingDummySnapshot Missing => default;

            public bool Exists;
            public string UnitName;
            public float CurrentHealth;
            public float MaxHealth;
            public float HealthRegen;
            public float Defense;
            public bool HasMana;
            public float CurrentMana;
            public float MaxMana;
            public float ManaRegen;
            public float AttackPower;
            public float SkillRange;
            public float ActionSpeedBonus;
            public float ChantSpeedBonus;
            public float MoveSpeed;
            public float MaxAcceleration;
            public int ActiveBuffCount;
        }
    }
}
