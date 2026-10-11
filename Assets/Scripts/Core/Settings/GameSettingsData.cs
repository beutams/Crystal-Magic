using System;
using UnityEngine;

namespace CrystalMagic.Core
{
    [Serializable]
    public sealed class GameSettingsData
    {
        public float MasterVolume = 1f;
        public float BgmVolume = 1f;
        public float SfxVolume = 1f;
        public float ScreenShakeScale = 1f;
        public GameLanguage Language = GameLanguage.ChineseSimplified;
        public bool Fullscreen = true;
        // Zero means an older settings file: adopt the current window on first load.
        public int ResolutionWidth;
        public int ResolutionHeight;
        // Applied only when a new save is created; never overrides an existing save's progress.
        public bool SkipTutorial;

        public void Clamp()
        {
            MasterVolume = Mathf.Clamp01(MasterVolume);
            BgmVolume = Mathf.Clamp01(BgmVolume);
            SfxVolume = Mathf.Clamp01(SfxVolume);
            ScreenShakeScale = Mathf.Max(0f, ScreenShakeScale);

            if (!Enum.IsDefined(typeof(GameLanguage), Language))
                Language = GameLanguage.ChineseSimplified;
            if (ResolutionWidth < 640 || ResolutionHeight < 360 || ResolutionWidth > 16384 || ResolutionHeight > 16384)
                ResolutionWidth = ResolutionHeight = 0;
        }

        public GameSettingsData Clone()
        {
            return new GameSettingsData
            {
                MasterVolume = MasterVolume,
                BgmVolume = BgmVolume,
                SfxVolume = SfxVolume,
                ScreenShakeScale = ScreenShakeScale,
                Language = Language,
                Fullscreen = Fullscreen,
                ResolutionWidth = ResolutionWidth,
                ResolutionHeight = ResolutionHeight,
                SkipTutorial = SkipTutorial,
            };
        }

        public static GameSettingsData CreateDefault()
        {
            return new GameSettingsData();
        }
    }
}
