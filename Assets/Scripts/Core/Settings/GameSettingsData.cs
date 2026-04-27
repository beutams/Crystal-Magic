using System;
using UnityEngine;

namespace CrystalMagic.Core
{
    [Serializable]
    public sealed class GameSettingsData
    {
        public float MasterVolume = 1f;
        public float BgmVolume = 1f;
        public float UnitVolume = 1f;
        public float UIVolume = 1f;
        public float ScreenShakeScale = 1f;

        public void Clamp()
        {
            MasterVolume = Mathf.Clamp01(MasterVolume);
            BgmVolume = Mathf.Clamp01(BgmVolume);
            UnitVolume = Mathf.Clamp01(UnitVolume);
            UIVolume = Mathf.Clamp01(UIVolume);
            ScreenShakeScale = Mathf.Max(0f, ScreenShakeScale);
        }

        public GameSettingsData Clone()
        {
            return new GameSettingsData
            {
                MasterVolume = MasterVolume,
                BgmVolume = BgmVolume,
                UnitVolume = UnitVolume,
                UIVolume = UIVolume,
                ScreenShakeScale = ScreenShakeScale,
            };
        }

        public static GameSettingsData CreateDefault()
        {
            return new GameSettingsData();
        }
    }
}
