using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class BattleUIModel : UIModelBase
    {
        public const string DataChangedEventName = "BattleUIModel.DataChanged";

        private readonly List<BattleSkillDisplayData> _skillItems = new();
        private Entity _cachedPlayerEntity = Entity.Null;
        private float _hpRatio = 1f;
        private float _mpRatio = 1f;
        private float _currentHp;
        private float _maxHp;
        private float _currentMp;
        private float _maxMp;

        public override string ChangedEventName => DataChangedEventName;
        public IReadOnlyList<BattleSkillDisplayData> SkillItems => _skillItems;
        public float HpRatio => _hpRatio;
        public float MpRatio => _mpRatio;
        public float CurrentHp => _currentHp;
        public float MaxHp => _maxHp;
        public float CurrentMp => _currentMp;
        public float MaxMp => _maxMp;

        public void Refresh()
        {
            RebuildState(publishIfChanged: false);
            PublishChanged();
        }

        public void RefreshRuntime()
        {
            RebuildState(publishIfChanged: true);
        }

        private void RebuildState(bool publishIfChanged)
        {
            SkillCData skillConfig = SaveDataComponent.Instance.GetSkillData();
            RuntimeSkillData runtimeSkillData = RuntimeDataComponent.Instance.GetSkillData();
            List<BattleSkillDisplayData> nextItems = BuildSkillItems(skillConfig, runtimeSkillData, out int selectedChainIndex);

            PlayerCombatSnapshot snapshot = ReadPlayerSnapshot(selectedChainIndex);
            ApplyRuntimeState(nextItems, snapshot);

            float nextHpRatio = snapshot.HasHealth ? snapshot.HpRatio : 1f;
            float nextMpRatio = snapshot.HasMana ? snapshot.MpRatio : 1f;
            float nextCurrentHp = snapshot.HasHealth ? snapshot.CurrentHealth : 0f;
            float nextMaxHp = snapshot.HasHealth ? snapshot.MaxHealth : 0f;
            float nextCurrentMp = snapshot.HasMana ? snapshot.CurrentMana : 0f;
            float nextMaxMp = snapshot.HasMana ? snapshot.MaxMana : 0f;

            bool changed = !AreSkillItemsEqual(_skillItems, nextItems)
                || !Mathf.Approximately(_hpRatio, nextHpRatio)
                || !Mathf.Approximately(_mpRatio, nextMpRatio)
                || !Mathf.Approximately(_currentHp, nextCurrentHp)
                || !Mathf.Approximately(_maxHp, nextMaxHp)
                || !Mathf.Approximately(_currentMp, nextCurrentMp)
                || !Mathf.Approximately(_maxMp, nextMaxMp);

            if (!changed)
                return;

            _skillItems.Clear();
            _skillItems.AddRange(nextItems);
            _hpRatio = nextHpRatio;
            _mpRatio = nextMpRatio;
            _currentHp = nextCurrentHp;
            _maxHp = nextMaxHp;
            _currentMp = nextCurrentMp;
            _maxMp = nextMaxMp;

            if (publishIfChanged)
                PublishChanged();
        }

        private static List<BattleSkillDisplayData> BuildSkillItems(SkillCData skillConfig, RuntimeSkillData runtimeSkillData, out int selectedChainIndex)
        {
            selectedChainIndex = 0;
            List<BattleSkillDisplayData> items = new();

            if (skillConfig?.Chains == null || skillConfig.Chains.Length == 0)
                return items;

            selectedChainIndex = Mathf.Clamp(runtimeSkillData?.CurrentSkillChainIndex ?? 0, 0, skillConfig.Chains.Length - 1);
            SkillChainData chain = skillConfig.Chains[selectedChainIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null)
                return items;

            for (int i = 0; i < chain.Slots.Count; i++)
            {
                SkillChainSlotData slot = chain.Slots[i];
                int skillStoneItemId = slot?.SkillStoneItemId ?? 0;
                SkillData skillData = SkillChainResolver.GetSkillDataBySkillStoneItemId(skillStoneItemId);
                SkillEffectData skillEffectData = slot != null && slot.SkillEffectId > 0
                    ? DataComponent.Instance.Get<SkillEffectData>(slot.SkillEffectId)
                    : null;

                items.Add(new BattleSkillDisplayData
                {
                    DisplayIndex = i + 1,
                    SkillIndex = i,
                    SkillId = skillData != null ? skillData.Id : 0,
                    SkillIconPath = skillData != null ? skillData.IconPath : string.Empty,
                    EffectIconPath = skillEffectData != null ? skillEffectData.IconPath : string.Empty,
                });
            }

            return items;
        }

        private static void ApplyRuntimeState(List<BattleSkillDisplayData> items, PlayerCombatSnapshot snapshot)
        {
            if (!snapshot.HasCast || !snapshot.IsCasting || snapshot.CurrentChainIndex < 0)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                BattleSkillDisplayData item = items[i];
                bool isSelected = snapshot.CurrentChainIndex == snapshot.SelectedChainIndex && item.SkillIndex == snapshot.CurrentSkillIndex;
                item.IsSelected = isSelected;
                item.ShowChantProgress = isSelected && snapshot.Phase == SkillCastPhase.Chanting && snapshot.PhaseDuration > 0f;
                item.ChantProgress = item.ShowChantProgress
                    ? Mathf.Clamp01(snapshot.PhaseElapsed / snapshot.PhaseDuration)
                    : 0f;
            }
        }

        private PlayerCombatSnapshot ReadPlayerSnapshot(int selectedChainIndex)
        {
            PlayerCombatSnapshot snapshot = new PlayerCombatSnapshot
            {
                SelectedChainIndex = selectedChainIndex,
            };

            if (!TryGetPlayerEntity(out EntityManager entityManager, out Entity player))
                return snapshot;

            if (entityManager.HasComponent<UnitCastComponent>(player))
            {
                UnitCastComponent cast = entityManager.GetComponentData<UnitCastComponent>(player);
                snapshot.HasCast = true;
                snapshot.IsCasting = cast.IsCasting;
                snapshot.CurrentChainIndex = cast.CurrentChainIndex;
                snapshot.CurrentSkillIndex = cast.CurrentSkillIndex;
                snapshot.Phase = cast.Phase;
                snapshot.PhaseElapsed = cast.PhaseElapsed;
                snapshot.PhaseDuration = cast.PhaseDuration;
            }

            if (entityManager.HasComponent<UnitVitalityComponent>(player))
            {
                UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(player);
                float maxHealth = Mathf.Max(vitality.RealMaxHealth, 0.0001f);
                snapshot.HasHealth = true;
                snapshot.CurrentHealth = vitality.CurrentHealth;
                snapshot.MaxHealth = vitality.RealMaxHealth;
                snapshot.HpRatio = Mathf.Clamp01(vitality.CurrentHealth / maxHealth);
            }

            if (entityManager.HasComponent<UnitManaComponent>(player))
            {
                UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(player);
                float maxMana = Mathf.Max(mana.RealMaxMp, 0.0001f);
                snapshot.HasMana = true;
                snapshot.CurrentMana = mana.CurrentMana;
                snapshot.MaxMana = mana.RealMaxMp;
                snapshot.MpRatio = Mathf.Clamp01(mana.CurrentMana / maxMana);
            }

            return snapshot;
        }

        private bool TryGetPlayerEntity(out EntityManager entityManager, out Entity player)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                player = Entity.Null;
                return false;
            }

            entityManager = world.EntityManager;
            if (_cachedPlayerEntity != Entity.Null &&
                entityManager.Exists(_cachedPlayerEntity) &&
                entityManager.HasComponent<PlayerTag>(_cachedPlayerEntity))
            {
                player = _cachedPlayerEntity;
                return true;
            }

            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length <= 0)
            {
                player = Entity.Null;
                return false;
            }

            _cachedPlayerEntity = entities[0];
            player = _cachedPlayerEntity;
            return true;
        }

        private void PublishChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }

        private static bool AreSkillItemsEqual(IReadOnlyList<BattleSkillDisplayData> left, IReadOnlyList<BattleSkillDisplayData> right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null || left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                BattleSkillDisplayData a = left[i];
                BattleSkillDisplayData b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                        return false;
                    continue;
                }

                if (a.DisplayIndex != b.DisplayIndex ||
                    a.SkillIndex != b.SkillIndex ||
                    a.SkillId != b.SkillId ||
                    a.IsSelected != b.IsSelected ||
                    a.ShowChantProgress != b.ShowChantProgress ||
                    !Mathf.Approximately(a.ChantProgress, b.ChantProgress) ||
                    !string.Equals(a.SkillIconPath, b.SkillIconPath, System.StringComparison.Ordinal) ||
                    !string.Equals(a.EffectIconPath, b.EffectIconPath, System.StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public sealed class BattleSkillDisplayData
    {
        public int DisplayIndex;
        public int SkillIndex;
        public int SkillId;
        public string SkillIconPath;
        public string EffectIconPath;
        public bool IsSelected;
        public bool ShowChantProgress;
        public float ChantProgress;
    }

    internal struct PlayerCombatSnapshot
    {
        public int SelectedChainIndex;
        public bool HasCast;
        public bool IsCasting;
        public int CurrentChainIndex;
        public int CurrentSkillIndex;
        public SkillCastPhase Phase;
        public float PhaseElapsed;
        public float PhaseDuration;
        public bool HasHealth;
        public float CurrentHealth;
        public float MaxHealth;
        public float HpRatio;
        public bool HasMana;
        public float CurrentMana;
        public float MaxMana;
        public float MpRatio;
    }
}
