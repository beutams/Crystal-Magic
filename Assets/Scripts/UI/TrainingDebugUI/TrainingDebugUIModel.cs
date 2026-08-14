using System.Collections.Generic;
using System.Text;
using CrystalMagic.Core;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.UI
{
    public sealed class TrainingDebugUIModel : UIModelBase
    {
        public const string DataChangedEventName = "TrainingDebugUIModel.DataChanged";

        private readonly List<TrainingDebugUnitSnapshot> _units = new();
        private World _world;
        private EntityQuery _unitQuery;
        private Entity _selectedEntity = Entity.Null;
        private float _nextRefreshTime;
        private int _lastResultVersion = -1;
        private string _resultMessage = string.Empty;
        private string _inspectorText = "Select a unit from the list.";

        public override string ChangedEventName => DataChangedEventName;
        public bool IsExpanded { get; private set; }
        public bool IsUnitControlOpen { get; private set; }
        public IReadOnlyList<TrainingDebugUnitSnapshot> Units => _units;
        public string ResultMessage => _resultMessage;
        public string InspectorText => _inspectorText;
        public Entity SelectedEntity => _selectedEntity;

        public void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
            if (!IsExpanded)
                IsUnitControlOpen = false;

            PublishChanged();
        }

        public void OpenUnitControl()
        {
            IsExpanded = true;
            IsUnitControlOpen = true;
            RefreshRuntime(true);
        }

        public void CloseUnitControl()
        {
            IsUnitControlOpen = false;
            PublishChanged();
        }

        public void SelectUnit(EntitySelection selection)
        {
            _selectedEntity = new Entity { Index = selection.Index, Version = selection.Version };
            RefreshRuntime(true);
        }

        public void RefreshRuntime(bool force = false)
        {
            float now = Time.unscaledTime;
            if (!force && now < _nextRefreshTime)
                return;

            _nextRefreshTime = now + 0.2f;
            bool changed = RefreshResult();
            if (IsUnitControlOpen)
                changed |= RefreshUnits();

            if (changed || force)
                PublishChanged();
        }

        public override void Dispose()
        {
            ReleaseQuery();
            _units.Clear();
            _selectedEntity = Entity.Null;
            base.Dispose();
        }

        private bool RefreshResult()
        {
            if (_lastResultVersion == TrainingDebugCommandQueue.ResultVersion)
                return false;

            _lastResultVersion = TrainingDebugCommandQueue.ResultVersion;
            _resultMessage = TrainingDebugCommandQueue.LastResult;
            return true;
        }

        private bool RefreshUnits()
        {
            if (!EnsureQuery(out EntityManager entityManager))
            {
                if (_units.Count == 0 && _selectedEntity == Entity.Null)
                    return false;

                _units.Clear();
                _selectedEntity = Entity.Null;
                _inspectorText = "No active ECS world.";
                return true;
            }

            List<TrainingDebugUnitSnapshot> nextUnits = new();
            using NativeArray<Entity> entities = _unitQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                UnitStateMachineComponent stateMachine = entityManager.GetComponentObject<UnitStateMachineComponent>(entity);
                if (stateMachine == null)
                    continue;

                TrainingDebugUnitSnapshot snapshot = new()
                {
                    Entity = entity,
                    UnitName = string.IsNullOrWhiteSpace(stateMachine.UnitName) ? entity.ToString() : stateMachine.UnitName,
                    StateName = stateMachine.CurrentStateName ?? "None",
                    HasAI = entityManager.HasComponent<UnitBehaviorTreeComponent>(entity),
                    TransitionCount = GetTransitionCount(stateMachine),
                    IsSelected = entity == _selectedEntity,
                };

                if (entityManager.HasComponent<UnitVitalityComponent>(entity))
                {
                    UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(entity);
                    snapshot.CurrentHealth = vitality.CurrentHealth;
                    snapshot.MaxHealth = vitality.RealMaxHealth;
                }

                nextUnits.Add(snapshot);
            }

            if (_selectedEntity != Entity.Null && !entityManager.Exists(_selectedEntity))
                _selectedEntity = Entity.Null;

            bool changed = !HasSameUnits(nextUnits);
            _units.Clear();
            _units.AddRange(nextUnits);
            string nextInspector = BuildInspectorText(entityManager);
            if (!string.Equals(_inspectorText, nextInspector, System.StringComparison.Ordinal))
            {
                _inspectorText = nextInspector;
                changed = true;
            }

            return changed;
        }

        private bool EnsureQuery(out EntityManager entityManager)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                return false;
            }

            if (_world != world)
            {
                ReleaseQuery();
                _world = world;
                _unitQuery = world.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<UnitStateMachineComponent>() },
                    None = new[]
                    {
                        ComponentType.ReadOnly<PlayerTag>(),
                        ComponentType.ReadOnly<Prefab>(),
                    },
                });
            }

            entityManager = world.EntityManager;
            return true;
        }

        private void ReleaseQuery()
        {
            if (_world != null && _world.IsCreated)
                _unitQuery.Dispose();

            _world = null;
            _unitQuery = default;
        }

        private string BuildInspectorText(EntityManager entityManager)
        {
            if (_selectedEntity == Entity.Null || !entityManager.Exists(_selectedEntity) ||
                !entityManager.HasComponent<UnitStateMachineComponent>(_selectedEntity))
            {
                return "Select a unit from the list.";
            }

            UnitStateMachineComponent stateMachine = entityManager.GetComponentObject<UnitStateMachineComponent>(_selectedEntity);
            if (stateMachine == null)
                return "Selected unit state machine is unavailable.";

            StringBuilder builder = new(256);
            builder.AppendLine($"Unit: {stateMachine.UnitName}");
            builder.AppendLine($"Entity: {_selectedEntity}");
            builder.AppendLine($"State: {stateMachine.CurrentStateName}");
            builder.AppendLine($"AI: {(entityManager.HasComponent<UnitBehaviorTreeComponent>(_selectedEntity) ? "Active" : "Removed")}");
            builder.AppendLine($"Transitions: {GetTransitionCount(stateMachine)}");

            if (entityManager.HasComponent<UnitFacingComponent>(_selectedEntity))
            {
                UnitFacingComponent facing = entityManager.GetComponentData<UnitFacingComponent>(_selectedEntity);
                builder.AppendLine($"Facing: ({facing.Direction.x:0.##}, {facing.Direction.y:0.##})");
            }

            if (entityManager.HasComponent<UnitAnimationComponent>(_selectedEntity))
            {
                UnitAnimationComponent animation = entityManager.GetComponentData<UnitAnimationComponent>(_selectedEntity);
                builder.AppendLine($"Animation: Clip {animation.ClipId}, Frame {animation.FrameIndex}");
            }

            if (entityManager.HasComponent<UnitVitalityComponent>(_selectedEntity))
            {
                UnitVitalityComponent vitality = entityManager.GetComponentData<UnitVitalityComponent>(_selectedEntity);
                builder.AppendLine($"HP: {vitality.CurrentHealth:0.##} / {vitality.RealMaxHealth:0.##}");
            }

            if (entityManager.HasComponent<UnitSkillComponent>(_selectedEntity))
            {
                UnitSkillComponent skills = entityManager.GetComponentData<UnitSkillComponent>(_selectedEntity);
                builder.Append("SkillIds: ");
                for (int i = 0; i < skills.Skills.Length; i++)
                {
                    if (i > 0)
                        builder.Append(", ");

                    builder.Append(skills.Skills[i].SkillId);
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private bool HasSameUnits(IReadOnlyList<TrainingDebugUnitSnapshot> nextUnits)
        {
            if (_units.Count != nextUnits.Count)
                return false;

            for (int i = 0; i < _units.Count; i++)
            {
                TrainingDebugUnitSnapshot current = _units[i];
                TrainingDebugUnitSnapshot next = nextUnits[i];
                if (current.Entity != next.Entity || current.UnitName != next.UnitName || current.StateName != next.StateName ||
                    current.HasAI != next.HasAI || current.TransitionCount != next.TransitionCount ||
                    !Mathf.Approximately(current.CurrentHealth, next.CurrentHealth) || !Mathf.Approximately(current.MaxHealth, next.MaxHealth) ||
                    current.IsSelected != next.IsSelected)
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetTransitionCount(UnitStateMachineComponent stateMachine)
        {
            if (stateMachine?.StateInstances == null)
                return 0;

            int count = 0;
            foreach (AUnitState state in stateMachine.StateInstances.Values)
                count += state?.transitions?.Count ?? 0;

            return count;
        }

        private void PublishChanged()
        {
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }
    }

    public sealed class TrainingDebugUnitSnapshot
    {
        public Entity Entity;
        public string UnitName;
        public string StateName;
        public float CurrentHealth;
        public float MaxHealth;
        public bool HasAI;
        public int TransitionCount;
        public bool IsSelected;
    }
}
