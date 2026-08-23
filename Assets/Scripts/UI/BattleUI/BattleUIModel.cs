using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
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
        private readonly List<BattlePropShortcutDisplayData> _propShortcutItems = new();
        private Entity _cachedPlayerEntity = Entity.Null;
        private float _hpRatio = 1f;
        private float _mpRatio = 1f;
        private float _currentHp;
        private float _maxHp;
        private float _currentMp;
        private float _maxMp;

        public override string ChangedEventName => DataChangedEventName;
        public IReadOnlyList<BattleSkillDisplayData> SkillItems => _skillItems;
        public IReadOnlyList<BattlePropShortcutDisplayData> PropShortcutItems => _propShortcutItems;
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
            CharacterPropData propConfig = SaveDataComponent.Instance.GetCharacterPropData();
            RuntimeSkillData runtimeSkillData = RuntimeDataComponent.Instance.GetSkillData();
            RuntimePropData runtimePropData = RuntimeDataComponent.Instance.GetPropData();
            List<BattleSkillDisplayData> nextItems = BuildSkillItems(skillConfig, runtimeSkillData);
            List<BattlePropShortcutDisplayData> nextPropItems = BuildPropShortcutItems(propConfig, runtimePropData);

            PlayerCombatSnapshot snapshot = ReadPlayerSnapshot();

            float nextHpRatio = snapshot.HasHealth ? snapshot.HpRatio : 1f;
            float nextMpRatio = snapshot.HasMana ? snapshot.MpRatio : 1f;
            float nextCurrentHp = snapshot.HasHealth ? snapshot.CurrentHealth : 0f;
            float nextMaxHp = snapshot.HasHealth ? snapshot.MaxHealth : 0f;
            float nextCurrentMp = snapshot.HasMana ? snapshot.CurrentMana : 0f;
            float nextMaxMp = snapshot.HasMana ? snapshot.MaxMana : 0f;

            bool changed = !AreSkillItemsEqual(_skillItems, nextItems)
                || !ArePropShortcutItemsEqual(_propShortcutItems, nextPropItems)
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
            _propShortcutItems.Clear();
            _propShortcutItems.AddRange(nextPropItems);
            _hpRatio = nextHpRatio;
            _mpRatio = nextMpRatio;
            _currentHp = nextCurrentHp;
            _maxHp = nextMaxHp;
            _currentMp = nextCurrentMp;
            _maxMp = nextMaxMp;

            if (publishIfChanged)
                PublishChanged();
        }

        private static List<BattleSkillDisplayData> BuildSkillItems(SkillCData skillConfig, RuntimeSkillData runtimeSkillData)
        {
            List<BattleSkillDisplayData> items = new();

            if (skillConfig?.Chains == null || skillConfig.Chains.Length == 0)
                return items;

            int selectedChainIndex = Mathf.Clamp(runtimeSkillData?.CurrentSkillChainIndex ?? 0, 0, skillConfig.Chains.Length - 1);
            SkillChainData chain = skillConfig.Chains[selectedChainIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null)
                return items;

            for (int i = 0; i < chain.Slots.Count; i++)
            {
                SkillChainSlotData slot = chain.Slots[i];
                int skillStoneItemId = slot?.SkillStoneItemId ?? -1;
                SkillData skillData = SkillChainResolver.GetSkillDataBySkillStoneItemId(skillStoneItemId);
                SkillAdditionData skillAdditionData = slot != null && slot.SkillAdditionId >= 0
                    ? DataComponent.Instance.Get<SkillAdditionData>(slot.SkillAdditionId)
                    : null;

                items.Add(new BattleSkillDisplayData
                {
                    DisplayIndex = i + 1,
                    SkillIndex = i,
                    SkillId = skillData != null ? skillData.Id : -1,
                    SkillIconPath = skillData != null ? skillData.IconPath : string.Empty,
                    AdditionIconPath = skillAdditionData != null ? skillAdditionData.IconPath : string.Empty,
                });
            }

            return items;
        }

        private static List<BattlePropShortcutDisplayData> BuildPropShortcutItems(CharacterPropData propConfig, RuntimePropData runtimePropData)
        {
            List<BattlePropShortcutDisplayData> items = new();
            if (propConfig?.ShortcutSlotIndexes == null)
                return items;

            float cooldownRemaining = Mathf.Max(0f, runtimePropData?.SharedCooldownRemaining ?? 0f);
            GameConfig config = ConfigComponent.Instance.Get<GameConfig>();
            float cooldownDuration = Mathf.Max(0f, config.BattlePropSharedCooldownSeconds);
            float cooldownRatio = cooldownDuration > 0f
                ? Mathf.Clamp01(cooldownRemaining / cooldownDuration)
                : 0f;

            for (int i = 0; i < propConfig.ShortcutSlotIndexes.Length; i++)
            {
                int propSlotIndex = propConfig.ShortcutSlotIndexes[i];
                CharacterPropSlotData propSlot = propConfig.Slots != null &&
                                                 propSlotIndex >= 0 &&
                                                 propSlotIndex < propConfig.Slots.Count
                    ? propConfig.Slots[propSlotIndex]
                    : null;
                int itemId = propSlot != null && !propSlot.IsEmpty ? propSlot.ItemId : -1;
                ItemData itemData = itemId >= 0 ? DataComponent.Instance.Get<ItemData>(itemId) : null;

                items.Add(new BattlePropShortcutDisplayData
                {
                    DisplayIndex = i + 1,
                    ShortcutIndex = i,
                    PropSlotIndex = propSlotIndex,
                    ItemId = itemId,
                    Count = propSlot != null && !propSlot.IsEmpty ? propSlot.Quantity : 0,
                    CarryLimit = itemId >= 0 ? PropInventoryUtility.GetCarryLimit(itemId) : 0,
                    Name = itemData != null ? itemData.Name : string.Empty,
                    IconPath = itemData != null ? itemData.IconPath : string.Empty,
                    CooldownRemaining = cooldownRemaining,
                    CooldownRatio = cooldownRatio,
                });
            }

            return items;
        }

        private PlayerCombatSnapshot ReadPlayerSnapshot()
        {
            PlayerCombatSnapshot snapshot = new();

            if (!TryGetPlayerEntity(out EntityManager entityManager, out Entity player))
                return snapshot;

            if (entityManager.HasComponent<UnitVitalityComponent>(player))
            {
                UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(player);
                float resolvedMaxHealth = UnitModifierResolver.GetMaxHealth(entityManager, player);
                float maxHealth = Mathf.Max(resolvedMaxHealth, 0.0001f);
                snapshot.HasHealth = true;
                snapshot.CurrentHealth = vitality.CurrentHealth;
                snapshot.MaxHealth = resolvedMaxHealth;
                snapshot.HpRatio = Mathf.Clamp01(vitality.CurrentHealth / maxHealth);
            }

            if (entityManager.HasComponent<UnitManaComponent>(player))
            {
                UnitManaComponent mana = entityManager.GetComponentData<UnitManaComponent>(player);
                float resolvedMaxMana = UnitModifierResolver.GetMaxMp(entityManager, player);
                float maxMana = Mathf.Max(resolvedMaxMana, 0.0001f);
                snapshot.HasMana = true;
                snapshot.CurrentMana = mana.CurrentMana;
                snapshot.MaxMana = resolvedMaxMana;
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
                entityManager.HasComponent<UnitFactionComponent>(_cachedPlayerEntity) &&
                UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(_cachedPlayerEntity).Value))
            {
                player = _cachedPlayerEntity;
                return true;
            }

            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitFactionComponent>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(entity).Value))
                    continue;

                _cachedPlayerEntity = entity;
                player = entity;
                return true;
            }

            player = Entity.Null;
            return false;
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
                    !string.Equals(a.AdditionIconPath, b.AdditionIconPath, System.StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ArePropShortcutItemsEqual(IReadOnlyList<BattlePropShortcutDisplayData> left, IReadOnlyList<BattlePropShortcutDisplayData> right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null || left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                BattlePropShortcutDisplayData a = left[i];
                BattlePropShortcutDisplayData b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                        return false;
                    continue;
                }

                if (a.DisplayIndex != b.DisplayIndex ||
                    a.ShortcutIndex != b.ShortcutIndex ||
                    a.PropSlotIndex != b.PropSlotIndex ||
                    a.ItemId != b.ItemId ||
                    a.Count != b.Count ||
                    a.CarryLimit != b.CarryLimit ||
                    !Mathf.Approximately(a.CooldownRemaining, b.CooldownRemaining) ||
                    !Mathf.Approximately(a.CooldownRatio, b.CooldownRatio) ||
                    !string.Equals(a.Name, b.Name, System.StringComparison.Ordinal) ||
                    !string.Equals(a.IconPath, b.IconPath, System.StringComparison.Ordinal))
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
        public string AdditionIconPath;
        public bool IsSelected;
        public bool ShowChantProgress;
        public float ChantProgress;
    }

    public sealed class BattlePropShortcutDisplayData
    {
        public int DisplayIndex;
        public int ShortcutIndex;
        public int PropSlotIndex;
        public int ItemId;
        public int Count;
        public int CarryLimit;
        public string Name;
        public string IconPath;
        public float CooldownRemaining;
        public float CooldownRatio;
    }

    internal struct PlayerCombatSnapshot
    {
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
