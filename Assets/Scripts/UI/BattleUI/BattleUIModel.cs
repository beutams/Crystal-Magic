using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.Skill;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class BattleUIModel : UIModelBase
    {
        public const string DataChangedEventName = "BattleUIModel.DataChanged";

        private readonly List<BattleSkillDisplayData> _skillItems = new();
        private readonly List<BattlePropShortcutDisplayData> _propShortcutItems = new();
        private readonly List<UnitHealthBarBuffDisplayData> _buffItems = new();
        private readonly List<UnitHealthBarBuffDisplayData> _nextBuffItems = new();
        private Entity _cachedPlayerEntity = Entity.Null;
        private float _hpRatio = 1f;
        private float _mpRatio = 1f;
        private float _currentHp;
        private float _currentMp;
        private bool _isChanting;
        private float _chantProgress;

        public override string ChangedEventName => DataChangedEventName;
        public IReadOnlyList<BattleSkillDisplayData> SkillItems => _skillItems;
        public IReadOnlyList<BattlePropShortcutDisplayData> PropShortcutItems => _propShortcutItems;
        public IReadOnlyList<UnitHealthBarBuffDisplayData> BuffItems => _buffItems;
        public float HpRatio => _hpRatio;
        public float MpRatio => _mpRatio;
        public float CurrentHp => _currentHp;
        public float CurrentMp => _currentMp;
        public bool IsChanting => _isChanting;
        public float ChantProgress => _chantProgress;

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
            PlayerCombatSnapshot snapshot = ReadPlayerSnapshot(_nextBuffItems);
            List<BattleSkillDisplayData> nextItems = BuildSkillItems(
                skillConfig,
                snapshot.SelectedSkillChainIndex,
                snapshot.CurrentSkillChainIndex,
                snapshot.CurrentSkillSlotIndex);
            List<BattlePropShortcutDisplayData> nextPropItems = BuildPropShortcutItems(propConfig, snapshot.PropCooldownRemaining);

            float nextHpRatio = snapshot.HasHealth ? snapshot.HpRatio : 1f;
            float nextMpRatio = snapshot.HasMana ? snapshot.MpRatio : 1f;
            float nextCurrentHp = snapshot.HasHealth ? snapshot.CurrentHealth : 0f;
            float nextCurrentMp = snapshot.HasMana ? snapshot.CurrentMana : 0f;

            bool changed = !AreSkillItemsEqual(_skillItems, nextItems)
                || !ArePropShortcutItemsEqual(_propShortcutItems, nextPropItems)
                || !AreBuffItemsEqual(_buffItems, _nextBuffItems)
                || !Mathf.Approximately(_hpRatio, nextHpRatio)
                || !Mathf.Approximately(_mpRatio, nextMpRatio)
                || !Mathf.Approximately(_currentHp, nextCurrentHp)
                || !Mathf.Approximately(_currentMp, nextCurrentMp)
                || _isChanting != snapshot.IsChanting
                || !Mathf.Approximately(_chantProgress, snapshot.ChantProgress);

            if (!changed)
                return;

            _skillItems.Clear();
            _skillItems.AddRange(nextItems);
            _propShortcutItems.Clear();
            _propShortcutItems.AddRange(nextPropItems);
            _buffItems.Clear();
            _buffItems.AddRange(_nextBuffItems);
            _hpRatio = nextHpRatio;
            _mpRatio = nextMpRatio;
            _currentHp = nextCurrentHp;
            _currentMp = nextCurrentMp;
            _isChanting = snapshot.IsChanting;
            _chantProgress = snapshot.ChantProgress;

            if (publishIfChanged)
                PublishChanged();
        }

        private static List<BattleSkillDisplayData> BuildSkillItems(
            SkillCData skillConfig,
            int selectedSkillChainIndex,
            int currentSkillChainIndex,
            int currentSkillSlotIndex)
        {
            List<BattleSkillDisplayData> items = new();

            if (skillConfig?.Chains == null || skillConfig.Chains.Length == 0)
                return items;

            int selectedChainIndex = Mathf.Clamp(selectedSkillChainIndex, 0, skillConfig.Chains.Length - 1);
            SkillChainData chain = skillConfig.Chains[selectedChainIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null)
                return items;

            int skillCount = Mathf.Min(chain.Slots.Count, SkillChainData.MaxLength);
            for (int i = 0; i < skillCount; i++)
            {
                SkillChainSlotData slot = chain.Slots[i];
                int skillStoneItemId = slot?.SkillStoneItemId ?? -1;
                SkillData skillData = SkillChainResolver.GetSkillDataBySkillStoneItemId(skillStoneItemId);

                items.Add(new BattleSkillDisplayData
                {
                    DisplayIndex = i + 1,
                    SkillIndex = i,
                    SkillId = skillData != null ? skillData.Id : -1,
                    SkillIconPath = skillData != null ? skillData.IconPath : string.Empty,
                    IsSelected = selectedChainIndex == currentSkillChainIndex &&
                                 i == currentSkillSlotIndex,
                });
            }

            return items;
        }

        private static List<BattlePropShortcutDisplayData> BuildPropShortcutItems(CharacterPropData propConfig, float cooldownRemaining)
        {
            List<BattlePropShortcutDisplayData> items = new();
            if (propConfig?.Slots == null)
                return items;

            cooldownRemaining = Mathf.Max(0f, cooldownRemaining);
            GameConfig config = ConfigComponent.Instance.Get<GameConfig>();
            float cooldownDuration = Mathf.Max(0f, config.BattlePropSharedCooldownSeconds);
            float cooldownRatio = cooldownDuration > 0f
                ? Mathf.Clamp01(cooldownRemaining / cooldownDuration)
                : 0f;

            int slotCount = Mathf.Min(3, propConfig.Slots.Count);
            for (int i = 0; i < slotCount; i++)
            {
                int propSlotIndex = i;
                CharacterPropSlotData propSlot = propConfig.Slots[i];
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

        private PlayerCombatSnapshot ReadPlayerSnapshot(List<UnitHealthBarBuffDisplayData> buffItems)
        {
            PlayerCombatSnapshot snapshot = new()
            {
                CurrentSkillChainIndex = -1,
                CurrentSkillSlotIndex = -1,
            };

            if (!TryGetPlayerEntity(out EntityManager entityManager, out Entity player))
                return snapshot;

            snapshot.IsChanting = TryGetChantProgress(entityManager, player, out snapshot.ChantProgress);

            UnitHealthBarManager.BuildVisibleBuffs(
                entityManager,
                player,
                buffItems,
                requirePlayerOrigin: false,
                out _);

            if (entityManager.HasComponent<UnitVitalityComponent>(player))
            {
                UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(player);
                float currentHealth = vitality.CurrentHealth;
                if (entityManager.HasComponent<NetworkIdentityComponent>(player) &&
                    Server.FrameManagerUtility.TryGet(entityManager, out Server.ClientFrameManager frame) &&
                    frame.TryGetPresentedHealth(entityManager.GetComponentData<NetworkIdentityComponent>(player).id,
                        out float presentedHealth))
                    currentHealth = presentedHealth;
                float resolvedMaxHealth = UnitModifierResolver.GetMaxHealth(entityManager, player);
                float maxHealth = Mathf.Max(resolvedMaxHealth, 0.0001f);
                snapshot.HasHealth = true;
                snapshot.CurrentHealth = currentHealth;
                snapshot.MaxHealth = resolvedMaxHealth;
                snapshot.HpRatio = Mathf.Clamp01(currentHealth / maxHealth);
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

            if (PlayerCurrentSkillUtility.TryGetCurrentSlot(entityManager, player, out _))
            {
                PlayerCurrentSkillComponent currentSkill = entityManager.GetComponentData<PlayerCurrentSkillComponent>(player);
                snapshot.CurrentSkillChainIndex = currentSkill.CurrentChainId;
                snapshot.CurrentSkillSlotIndex = currentSkill.CurrentSlotIndex;
            }

            if (entityManager.HasComponent<PlayerInputComponent>(player))
                snapshot.SelectedSkillChainIndex = entityManager.GetComponentData<PlayerInputComponent>(player).SkillChainIndex;

            if (entityManager.HasComponent<PlayerPropCooldownComponent>(player))
                snapshot.PropCooldownRemaining = entityManager.GetComponentData<PlayerPropCooldownComponent>(player).SharedCooldownRemaining;

            if (GameWorldManager.Role == GameWorldRole.Client &&
                entityManager.HasComponent<ClientCooldownPresentationComponent>(player))
            {
                snapshot.PropCooldownRemaining = entityManager
                    .GetComponentData<ClientCooldownPresentationComponent>(player)
                    .PropCooldownRemaining;
            }

            return snapshot;
        }

        private static bool TryGetChantProgress(EntityManager entityManager, Entity player, out float progress)
        {
            progress = 0f;
            if (!entityManager.HasComponent<UnitStateScriptComponent>(player) ||
                !entityManager.HasBuffer<StateScriptGraphStateElement>(player) ||
                !entityManager.HasBuffer<StateScriptNodeStateElement>(player) ||
                !PlayerCurrentSkillUtility.IsCasting(entityManager, player))
                return false;

            UnitStateScriptComponent component = entityManager.GetComponentData<UnitStateScriptComponent>(player);
            if (component.IsStoppedForDeath != 0 || component.DefinitionIndex < 0)
                return false;

            using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<StateScriptRuntimeRegistryComponent>());
            if (!query.TryGetSingleton(out StateScriptRuntimeRegistryComponent registry) ||
                !registry.Value.IsCreated || component.DefinitionIndex >= registry.Value.Value.Units.Length)
                return false;

            ref StateScriptUnitDefinitionBlob definition = ref registry.Value.Value.Units[component.DefinitionIndex];
            DynamicBuffer<StateScriptGraphStateElement> graphs = entityManager.GetBuffer<StateScriptGraphStateElement>(player, true);
            DynamicBuffer<StateScriptNodeStateElement> states = entityManager.GetBuffer<StateScriptNodeStateElement>(player, true);
            for (int graphIndex = 0; graphIndex < definition.Graphs.Length && graphIndex < graphs.Length; graphIndex++)
            {
                StateScriptGraphStateElement graphState = graphs[graphIndex];
                if (graphState.IsActive == 0)
                    continue;

                ref StateScriptGraphDefinitionBlob graph = ref definition.Graphs[graphIndex];
                for (int i = 0; i < graph.StateNodeIndices.Length; i++)
                {
                    int nodeIndex = graph.StateNodeIndices[i];
                    ref StateScriptNodeDefinition node = ref graph.Nodes[nodeIndex];
                    if (node.Type != StateScriptNodeRuntimeType.Timer)
                        continue;

                    int stateIndex = graphState.NodeStateStart + nodeIndex;
                    if (stateIndex < 0 || stateIndex >= states.Length)
                        continue;

                    StateScriptNodeStateElement state = states[stateIndex];
                    if (state.Status == StateScriptStateStatus.Stop || state.Auxiliary <= 0f)
                        continue;

                    // Identify the chant timer by its duration source, independent of graph/node names.
                    ref BehaviorExpressionBlob duration = ref graph.Expressions[node.ExpressionStart];
                    for (int instructionIndex = 0; instructionIndex < duration.Instructions.Length; instructionIndex++)
                    {
                        ExpressionInstruction instruction = duration.Instructions[instructionIndex];
                        if (instruction.Kind == ExpressionInstructionKind.Source &&
                            (instruction.SourceId == UnitSourceId.PlayerSkillGetSkillChantDuration ||
                             instruction.SourceId == UnitSourceId.PlayerSkillGetCurrentSkillChantDuration))
                        {
                            // Auxiliary is the evaluated duration, including speed and addition modifiers.
                            progress = Mathf.Clamp01(state.Time / state.Auxiliary);
                            return true;
                        }
                    }
                }
            }

            return false;
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
                (GameWorldManager.Role != GameWorldRole.Client ||
                 entityManager.HasComponent<NetworkPlayerComponent>(_cachedPlayerEntity)) &&
                UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(_cachedPlayerEntity).Value))
            {
                player = _cachedPlayerEntity;
                return true;
            }

            if (GameRuntimeStateUtility.TryGetPlayerEntity(entityManager, out Entity entity) &&
                entityManager.HasComponent<UnitFactionComponent>(entity) &&
                UnitFactionUtility.IsPlayer(entityManager.GetComponentData<UnitFactionComponent>(entity).Value))
            {
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
                    !string.Equals(a.SkillIconPath, b.SkillIconPath, System.StringComparison.Ordinal))
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

        private static bool AreBuffItemsEqual(
            IReadOnlyList<UnitHealthBarBuffDisplayData> left,
            IReadOnlyList<UnitHealthBarBuffDisplayData> right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null || left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                UnitHealthBarBuffDisplayData a = left[i];
                UnitHealthBarBuffDisplayData b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                        return false;
                    continue;
                }

                if (a.BuffId != b.BuffId ||
                    a.StackCount != b.StackCount ||
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
        public bool IsSelected;
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
        public int CurrentSkillChainIndex;
        public int CurrentSkillSlotIndex;
        public int SelectedSkillChainIndex;
        public float PropCooldownRemaining;
        public bool IsChanting;
        public float ChantProgress;
    }
}
