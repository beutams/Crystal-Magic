using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    [CreateAssetMenu(menuName = "Crystal Magic/Unit Animation Frame Library")]
    public sealed class UnitAnimationFrameLibrary : ScriptableObject
    {
        public List<UnitAnimationFrameTrack> Tracks = new();

        public UnitAnimationFrameTrack Find(string clipPath)
        {
            for (int i = 0; i < Tracks.Count; i++)
            {
                UnitAnimationFrameTrack track = Tracks[i];
                if (track != null && string.Equals(track.ClipPath, clipPath, StringComparison.Ordinal))
                    return track;
            }

            return null;
        }
    }

    [Serializable]
    public sealed class UnitAnimationFrameTrack
    {
        public string ClipPath;
        public AnimationClip SourceClip;
        public Sprite[] Sprites = Array.Empty<Sprite>();
        public float[] SpriteTimes = Array.Empty<float>();
        public float[] FlipXTimes = Array.Empty<float>();
        public float[] FlipXValues = Array.Empty<float>();
        public float Length;
        public bool IsLooping;

        public Sprite SampleSprite(float time)
        {
            if (Sprites == null || Sprites.Length == 0)
                return null;

            int index = SampleIndex(SpriteTimes, Sprites.Length, time);
            return Sprites[index];
        }

        public bool TrySampleFlipX(float time, out bool flipX)
        {
            if (FlipXValues == null || FlipXValues.Length == 0)
            {
                flipX = false;
                return false;
            }

            int index = SampleIndex(FlipXTimes, FlipXValues.Length, time);
            flipX = FlipXValues[index] >= 0.5f;
            return true;
        }

        private static int SampleIndex(float[] times, int valueCount, float time)
        {
            int count = Mathf.Min(times?.Length ?? 0, valueCount);
            if (count <= 1)
                return 0;

            int index = 0;
            for (int i = 1; i < count; i++)
            {
                if (times[i] > time)
                    break;

                index = i;
            }

            return index;
        }
    }
}
