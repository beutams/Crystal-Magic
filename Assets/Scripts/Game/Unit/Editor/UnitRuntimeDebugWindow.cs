using System;
using System.Collections.Generic;
using System.Linq;
using CrystalMagic.Core;
using CrystalMagic.Editor.Data;
using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Unit
{
    public class UnitRuntimeDebugWindow : EditorWindow
    {
        private const float UnitListWidth = 220f;
        private const float StatePanelWidth = 320f;
        private const float BehaviorPanelWidth = 360f;

        private readonly List<UnitRuntimeEntry> _unitEntries = new();
        private Vector2 _unitListScrollPos;
        private Vector2 _valueScrollPos;
        private Vector2 _stateScrollPos;
        private Vector2 _behaviorScrollPos;
        private int _selectedIndex = -1;
        private string _statusText = "Enter Play Mode and refresh to inspect runtime units.";

        private sealed class UnitRuntimeEntry
        {
            public Entity Entity;
            public string DisplayName;
            public string UnitName;
        }

        [MenuItem("Tools/Debug/Unit Runtime Debug")]
        public static void Open()
        {
            UnitRuntimeDebugWindow window = GetWindow<UnitRuntimeDebugWindow>("Unit Runtime Debug");
            window.minSize = new Vector2(1280f, 720f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshUnits();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            RefreshUnits();
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawUnitListPanel();
            DrawPanelDivider();
            DrawRuntimeValuePanel();
            DrawPanelDivider();
            DrawStateMachinePanel();
            DrawPanelDivider();
            DrawBehaviorTreePanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                RefreshUnits();

            GUILayout.Space(8f);
            EditorGUILayout.LabelField(_statusText, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawUnitListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(UnitListWidth));
            EditorGUILayout.LabelField($"Units ({_unitEntries.Count})", EditorStyles.boldLabel);

            _unitListScrollPos = EditorGUILayout.BeginScrollView(_unitListScrollPos);
            for (int i = 0; i < _unitEntries.Count; i++)
            {
                UnitRuntimeEntry entry = _unitEntries[i];
                bool isSelected = i == _selectedIndex;
                if (GUILayout.Toggle(isSelected, entry.DisplayName, "Button"))
                {
                    _selectedIndex = i;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeValuePanel()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Runtime Values", EditorStyles.boldLabel);

            if (!TryCreateContext(out UnitRuntimeDrawerContext context))
            {
                EditorGUILayout.HelpBox("Select a live unit to inspect runtime values.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Unit", string.IsNullOrWhiteSpace(context.UnitName) ? "(Unnamed)" : context.UnitName);
                EditorGUILayout.TextField("Entity", context.Entity.ToString());
                EditorGUILayout.TextField("UnitData", context.UnitData != null ? $"[{context.UnitData.Id}] {context.UnitData.Name}" : "None");
            }

            _valueScrollPos = EditorGUILayout.BeginScrollView(_valueScrollPos);
            IReadOnlyList<IUnitRuntimeAttributeDrawer> drawers = UnitRuntimeAttributeDrawerFactory.GetDrawers();
            bool hasAnyDrawer = false;
            for (int i = 0; i < drawers.Count; i++)
            {
                IUnitRuntimeAttributeDrawer drawer = drawers[i];
                if (!drawer.CanDraw(context))
                    continue;

                hasAnyDrawer = true;
                GUILayout.Space(8f);
                drawer.Draw(context);
            }

            if (!hasAnyDrawer)
                EditorGUILayout.HelpBox("This runtime unit does not expose any registered runtime drawers.", MessageType.Info);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawStateMachinePanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(StatePanelWidth));
            EditorGUILayout.LabelField("State Machine", EditorStyles.boldLabel);

            if (!TryGetSelectedEntityManager(out EntityManager entityManager, out Entity entity))
            {
                EditorGUILayout.HelpBox("Select a live unit to inspect state machine runtime state.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _stateScrollPos = EditorGUILayout.BeginScrollView(_stateScrollPos);
            if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitStateMachineComponent>(entity))
            {
                EditorGUILayout.HelpBox("This unit has no runtime state machine component.", MessageType.Info);
            }
            else
            {
                UnitStateMachineComponent stateMachine = entityManager.GetComponentObject<UnitStateMachineComponent>(entity);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Unit Name", stateMachine.UnitName ?? string.Empty);
                    EditorGUILayout.TextField("Current State", stateMachine.CurrentStateName ?? "None");
                    EditorGUILayout.TextField("Previous State", stateMachine.PreviousStateName ?? "None");
                    EditorGUILayout.FloatField("State Time", stateMachine.StateTime);
                }

                GUILayout.Space(8f);
                EditorGUILayout.HelpBox("State machine real-time graph highlighting will be wired into this panel next.", MessageType.None);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawBehaviorTreePanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(BehaviorPanelWidth));
            EditorGUILayout.LabelField("Behavior Tree", EditorStyles.boldLabel);

            if (!TryGetSelectedEntityManager(out EntityManager entityManager, out Entity entity))
            {
                EditorGUILayout.HelpBox("Select a live unit to inspect behavior tree runtime state.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _behaviorScrollPos = EditorGUILayout.BeginScrollView(_behaviorScrollPos);
            if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitBehaviorTreeComponent>(entity))
            {
                EditorGUILayout.HelpBox("This unit has no runtime behavior tree component.", MessageType.Info);
            }
            else
            {
                UnitBehaviorTreeComponent behaviorTree = entityManager.GetComponentObject<UnitBehaviorTreeComponent>(entity);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Unit Name", behaviorTree.UnitName ?? string.Empty);
                    EditorGUILayout.Toggle("Initialized", behaviorTree.IsInitialized);
                    EditorGUILayout.TextField("Current Node", behaviorTree.CurrentNodeName ?? "None");
                    EditorGUILayout.TextField("Last Status", behaviorTree.LastStatus ?? "None");
                }

                GUILayout.Space(8f);
                EditorGUILayout.HelpBox("Behavior tree real-time graph coloring and active-path visualization will be wired into this panel next.", MessageType.None);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void RefreshUnits()
        {
            _unitEntries.Clear();
            _selectedIndex = -1;

            if (!Application.isPlaying)
            {
                _statusText = "Enter Play Mode to inspect runtime units.";
                return;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                _statusText = "Runtime world is not available.";
                return;
            }

            EntityManager entityManager = world.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalTransform>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!IsRuntimeUnit(entityManager, entity))
                    continue;

                string unitName = GetUnitName(entityManager, entity);
                _unitEntries.Add(new UnitRuntimeEntry
                {
                    Entity = entity,
                    UnitName = unitName,
                    DisplayName = string.IsNullOrWhiteSpace(unitName) ? entity.ToString() : $"{unitName} ({entity})",
                });
            }

            _unitEntries.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal));
            _selectedIndex = _unitEntries.Count > 0 ? 0 : -1;
            _statusText = $"Loaded {_unitEntries.Count} live unit(s).";
        }

        private static bool IsRuntimeUnit(EntityManager entityManager, Entity entity)
        {
            return entityManager.HasComponent<UnitFactionComponent>(entity) ||
                   entityManager.HasComponent<UnitVitalityComponent>(entity) ||
                   entityManager.HasComponent<UnitAttackComponent>(entity) ||
                   entityManager.HasComponent<UnitMoveComponent>(entity) ||
                   entityManager.HasComponent<UnitManaComponent>(entity) ||
                   entityManager.HasComponent<UnitPerceptionComponent>(entity) ||
                   entityManager.HasComponent<UnitStateMachineComponent>(entity) ||
                   entityManager.HasComponent<UnitBehaviorTreeComponent>(entity);
        }

        private bool TryCreateContext(out UnitRuntimeDrawerContext context)
        {
            if (!TryGetSelectedEntityManager(out EntityManager entityManager, out Entity entity))
            {
                context = null;
                return false;
            }

            string unitName = GetUnitName(entityManager, entity);
            UnitData unitData = ResolveUnitData(unitName);
            context = new UnitRuntimeDrawerContext(entityManager, entity, unitName, unitData);
            return true;
        }

        private bool TryGetSelectedEntityManager(out EntityManager entityManager, out Entity entity)
        {
            entityManager = default;
            entity = Entity.Null;

            if (_selectedIndex < 0 || _selectedIndex >= _unitEntries.Count)
                return false;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            entityManager = world.EntityManager;
            entity = _unitEntries[_selectedIndex].Entity;
            return entity != Entity.Null && entityManager.Exists(entity);
        }

        private static string GetUnitName(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<UnitStateMachineComponent>(entity))
            {
                UnitStateMachineComponent stateMachine = entityManager.GetComponentObject<UnitStateMachineComponent>(entity);
                if (!string.IsNullOrWhiteSpace(stateMachine?.UnitName))
                    return stateMachine.UnitName;
            }

            if (entityManager.HasComponent<UnitBehaviorTreeComponent>(entity))
            {
                UnitBehaviorTreeComponent behaviorTree = entityManager.GetComponentObject<UnitBehaviorTreeComponent>(entity);
                if (!string.IsNullOrWhiteSpace(behaviorTree?.UnitName))
                    return behaviorTree.UnitName;
            }

            return string.Empty;
        }

        private static UnitData ResolveUnitData(string unitName)
        {
            if (string.IsNullOrWhiteSpace(unitName))
                return null;

            return EditorComponents.Data.Find<UnitData>(row => string.Equals(row.Name, unitName, StringComparison.Ordinal));
        }

        private static void DrawPanelDivider()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 0f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            rect.width = 1f;
            rect.yMin -= 4f;
            rect.yMax += 4f;
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
        }
    }
}
