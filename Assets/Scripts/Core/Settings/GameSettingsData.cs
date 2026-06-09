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

        public void Clamp()
        {
            MasterVolume = Mathf.Clamp01(MasterVolume);
            BgmVolume = Mathf.Clamp01(BgmVolume);
            SfxVolume = Mathf.Clamp01(SfxVolume);
            ScreenShakeScale = Mathf.Max(0f, ScreenShakeScale);
        }

        public GameSettingsData Clone()
        {
            return new GameSettingsData
            {
                MasterVolume = MasterVolume,
                BgmVolume = BgmVolume,
                SfxVolume = SfxVolume,
                ScreenShakeScale = ScreenShakeScale,
            };
        }

        public static GameSettingsData CreateDefault()
        {
            return new GameSettingsData();
        }
    }
}
