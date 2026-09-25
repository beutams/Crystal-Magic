using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrystalMagic.Editor
{
    internal static class GameWorldExecutionTargetEditorUtility
    {
        private static readonly TargetBadge[] s_badges =
        {
            new(GameWorldExecutionTarget.Standalone, "Standalone", new Color(0.30f, 0.78f, 0.38f)),
            new(GameWorldExecutionTarget.Client, "Client", new Color(0.25f, 0.61f, 0.94f)),
            new(GameWorldExecutionTarget.Server, "Server", new Color(0.95f, 0.52f, 0.20f)),
        };

        public static VisualElement CreateBadges(
            Func<GameWorldExecutionTarget> getTargets,
            Action<GameWorldExecutionTarget> setTargets,
            Action onChanged)
        {
            VisualElement container = new()
            {
                style =
                {
                    position = Position.Absolute,
                    right = 5f,
                    top = 5f,
                    flexDirection = FlexDirection.Row,
                },
            };
            VisualElement[] elements = new VisualElement[s_badges.Length];

            void Refresh()
            {
                GameWorldExecutionTarget targets = getTargets();
                for (int index = 0; index < s_badges.Length; index++)
                {
                    TargetBadge badge = s_badges[index];
                    bool enabled = (targets & badge.Target) != 0;
                    Color color = enabled
                        ? badge.Color
                        : Color.Lerp(new Color(0.15f, 0.15f, 0.15f, 1f), badge.Color, 0.22f);
                    elements[index].style.backgroundColor = color;
                    elements[index].tooltip = $"{badge.Label}: {(enabled ? "Enabled" : "Disabled")}";
                }
            }

            for (int index = 0; index < s_badges.Length; index++)
            {
                TargetBadge badge = s_badges[index];
                VisualElement element = new()
                {
                    style =
                    {
                        width = 10f,
                        height = 10f,
                        marginLeft = 3f,
                        borderTopLeftRadius = 2f,
                        borderTopRightRadius = 2f,
                        borderBottomLeftRadius = 2f,
                        borderBottomRightRadius = 2f,
                    },
                };
                element.RegisterCallback<ClickEvent>(evt =>
                {
                    setTargets(getTargets() ^ badge.Target);
                    Refresh();
                    onChanged?.Invoke();
                    evt.StopPropagation();
                });
                elements[index] = element;
                container.Add(element);
            }

            Refresh();
            container.userData = (Action)Refresh;
            return container;
        }

        public static void RefreshBadges(VisualElement container)
        {
            if (container?.userData is Action refresh)
                refresh();
        }

        public static GameWorldExecutionTarget DrawImGuiBadges(
            Rect headerRect,
            GameWorldExecutionTarget targets)
        {
            const float size = 10f;
            const float gap = 3f;
            float x = headerRect.xMax - 7f - s_badges.Length * size - (s_badges.Length - 1) * gap;
            float y = headerRect.y + (headerRect.height - size) * 0.5f;
            for (int index = 0; index < s_badges.Length; index++)
            {
                TargetBadge badge = s_badges[index];
                bool enabled = (targets & badge.Target) != 0;
                Color color = enabled
                    ? badge.Color
                    : Color.Lerp(new Color(0.15f, 0.15f, 0.15f, 1f), badge.Color, 0.22f);
                Rect badgeRect = new(x, y, size, size);
                EditorGUI.DrawRect(badgeRect, color);
                if (GUI.Button(
                        badgeRect,
                        new GUIContent(string.Empty, $"{badge.Label}: {(enabled ? "Enabled" : "Disabled")}"),
                        GUIStyle.none))
                {
                    targets ^= badge.Target;
                }
                x += size + gap;
            }
            return targets;
        }

        private readonly struct TargetBadge
        {
            public TargetBadge(GameWorldExecutionTarget target, string label, Color color)
            {
                Target = target;
                Label = label;
                Color = color;
            }

            public GameWorldExecutionTarget Target { get; }
            public string Label { get; }
            public Color Color { get; }
        }
    }
}
