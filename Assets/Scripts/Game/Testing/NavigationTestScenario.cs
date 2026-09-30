using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Game.Testing
{
    // Scene data only. The editor controller owns and disposes the isolated simulation.
    [DisallowMultipleComponent]
    public sealed class NavigationTestScenario : MonoBehaviour
    {
        public NavigationTestSettings Settings = NavigationTestSettings.CreateDefault();

        private void OnDrawGizmos()
        {
            if (Settings == null) return;
            Vector2 size = new(Settings.Width * Settings.CellSize, Settings.Height * Settings.CellSize);
            Gizmos.color = new Color(0.12f, 0.16f, 0.21f, 0.5f);
            Gizmos.DrawCube(Vector3.zero, new Vector3(size.x, size.y, 0.02f));
            Gizmos.color = new Color(0.45f, 0.55f, 0.65f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0.02f));
            if (Settings.Obstacles == null) return;
            Gizmos.color = new Color(0.48f, 0.53f, 0.61f);
            foreach (RectInt area in Settings.Obstacles)
            {
                Vector2 center = -size * 0.5f + (Vector2)area.center * Settings.CellSize;
                Gizmos.DrawCube(new Vector3(center.x, center.y, 0),
                    new Vector3(area.width * Settings.CellSize, area.height * Settings.CellSize, 0.1f));
            }
        }
    }

    [Serializable]
    public sealed class NavigationTestSettings
    {
        [Range(4, 128)] public int Width = 32;
        [Range(4, 128)] public int Height = 24;
        [Min(0.1f)] public float CellSize = 1f;
        public bool UseAStar = true;
        public bool UseOrca = true;
        [Range(0.005f, 0.05f)] public float FixedDeltaTime = 1f / 60f;
        [Min(0.01f)] public float StopDistance = 0.15f;
        [Min(0)] public float NeighborDistance = 4f;
        [Range(0, 32)] public int MaxNeighbors = 12;
        [Min(0.01f)] public float TimeHorizon = 0.8f;
        [Min(0)] public float RadiusPadding = 0.05f;
        public List<NavigationTestAgent> Agents = new();
        // Grid coordinates, with (0,0) at the bottom-left corner of the map.
        public List<RectInt> Obstacles = new();

        public static NavigationTestSettings CreateDefault()
        {
            var settings = new NavigationTestSettings();
            settings.Obstacles.Add(new RectInt(15, 3, 2, 7));
            settings.Obstacles.Add(new RectInt(15, 14, 2, 7));
            for (int side = 0; side < 2; side++)
            for (int row = 0; row < 4; row++)
            {
                float x = side == 0 ? -10.5f : 10.5f;
                float y = -4.5f + row * 3;
                settings.Agents.Add(new NavigationTestAgent
                {
                    Name = $"{(side == 0 ? "A" : "B")}{row + 1}",
                    Start = new Vector2(x, y), Destination = new Vector2(-x, y),
                    Color = side == 0 ? new Color(0.2f, 0.75f, 1) : new Color(1, 0.55f, 0.2f),
                });
            }
            return settings;
        }
    }

    [Serializable]
    public sealed class NavigationTestAgent
    {
        public string Name = "Unit";
        public Vector2 Start;
        public Vector2 Destination = new(5.5f, 0.5f);
        [Min(0.05f)] public float Radius = 0.35f;
        [Min(0.01f)] public float Speed = 4f;
        [Min(0.01f)] public float Acceleration = 30f;
        public Color Color = new(0.2f, 0.75f, 1);
    }
}
