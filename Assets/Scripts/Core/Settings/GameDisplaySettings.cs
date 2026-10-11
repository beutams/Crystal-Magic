using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core
{
    public static class GameDisplaySettings
    {
        private static readonly Vector2Int[] CommonResolutions =
        {
            new(1024, 768), new(1280, 720), new(1280, 800), new(1366, 768),
            new(1440, 900), new(1600, 900), new(1680, 1050), new(1920, 1080),
            new(1920, 1200), new(2560, 1080), new(2560, 1440), new(2560, 1600),
            new(3440, 1440), new(3840, 2160),
        };

        public static List<Vector2Int> GetResolutions(Vector2Int desktop, Vector2Int current)
        {
            var options = new List<Vector2Int>();
            foreach (Vector2Int resolution in CommonResolutions)
                Add(resolution);
            Add(desktop);
            Add(current);
            options.Sort((left, right) => left.x != right.x ? left.x.CompareTo(right.x) : left.y.CompareTo(right.y));
            return options;

            void Add(Vector2Int resolution)
            {
                if (resolution.x >= 640 && resolution.y >= 360 && resolution.x <= desktop.x &&
                    resolution.y <= desktop.y && !options.Contains(resolution))
                    options.Add(resolution);
            }
        }

        public static Vector2Int ShiftResolution(Vector2Int current, int offset, List<Vector2Int> options)
        {
            if (options.Count == 0 || offset == 0)
                return current;
            int index = options.IndexOf(current);
            if (index < 0)
                index = offset > 0 ? -1 : 0;
            int next = (int)(((long)index + offset) % options.Count);
            return options[(next + options.Count) % options.Count];
        }

        public static void Resolve(GameSettingsData settings, Vector2Int desktop, Vector2Int current, bool currentFullscreen)
        {
            settings.Clamp();
            if (settings.ResolutionWidth == 0 || settings.ResolutionHeight == 0)
            {
                settings.ResolutionWidth = Mathf.Max(640, current.x);
                settings.ResolutionHeight = Mathf.Max(360, current.y);
                settings.Fullscreen = currentFullscreen;
            }
            // A saved 4K window must still be reachable when reopening on a smaller monitor.
            if (settings.ResolutionWidth > desktop.x || settings.ResolutionHeight > desktop.y)
            {
                settings.ResolutionWidth = desktop.x;
                settings.ResolutionHeight = desktop.y;
            }
        }

        public static string ModeLabel(GameSettingsData settings) => settings.Language == GameLanguage.English
            ? (settings.Fullscreen ? "Fullscreen" : "Windowed")
            : (settings.Fullscreen ? "全屏" : "窗口");

        public static string ResolutionLabel(GameSettingsData settings) => settings.ResolutionWidth > 0
            ? $"{settings.ResolutionWidth} × {settings.ResolutionHeight}"
            : (settings.Language == GameLanguage.English ? "Current" : "当前");
    }
}
