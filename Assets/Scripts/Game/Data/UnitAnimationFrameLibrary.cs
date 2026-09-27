using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    [CreateAssetMenu(menuName = "Crystal Magic/Unit Animation Frame Library")]
    public sealed class UnitAnimationFrameLibrary : ScriptableObject
    {
        public List<UnitAnimationFrameTrack> Tracks = new();

        [NonSerialized]
        private Dictionary<string, UnitAnimationFrameTrack> _tracksByPath;

        [NonSerialized]
        private Dictionary<string, int> _trackIndicesByPath;

        [NonSerialized]
        private int _indexedTrackCount = -1;

        public UnitAnimationFrameTrack Find(string clipPath)
        {
            if (string.IsNullOrEmpty(clipPath))
                return null;

            EnsureTrackPathLookup();
            _tracksByPath.TryGetValue(clipPath, out UnitAnimationFrameTrack track);
            return track;
        }

        public UnitAnimationFrameTrack Find(AnimationClip clip)
        {
            if (clip == null)
                return null;

            for (int i = 0; i < Tracks.Count; i++)
            {
                UnitAnimationFrameTrack track = Tracks[i];
                if (track != null && track.SourceClip == clip)
                    return track;
            }

            return null;
        }

        public int FindTrackIndex(string clipPath)
        {
            if (string.IsNullOrEmpty(clipPath))
                return -1;

            EnsureTrackPathLookup();
            return _trackIndicesByPath.TryGetValue(clipPath, out int index) ? index : -1;
        }

        public UnitAnimationFrameTrack GetTrack(int trackId)
        {
            return trackId >= 0 && trackId < Tracks.Count ? Tracks[trackId] : null;
        }

        private void EnsureTrackPathLookup()
        {
            int trackCount = Tracks?.Count ?? 0;
            if (_tracksByPath != null && _indexedTrackCount == trackCount)
                return;

            _tracksByPath ??= new Dictionary<string, UnitAnimationFrameTrack>(trackCount, StringComparer.Ordinal);
            _trackIndicesByPath ??= new Dictionary<string, int>(trackCount, StringComparer.Ordinal);
            _tracksByPath.Clear();
            _trackIndicesByPath.Clear();
            for (int i = 0; i < trackCount; i++)
            {
                UnitAnimationFrameTrack track = Tracks[i];
                if (track == null || string.IsNullOrEmpty(track.ClipPath) || _tracksByPath.ContainsKey(track.ClipPath))
                    continue;

                _tracksByPath.Add(track.ClipPath, track);
                _trackIndicesByPath.Add(track.ClipPath, i);
            }

            _indexedTrackCount = trackCount;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _tracksByPath = null;
            _trackIndicesByPath = null;
            _indexedTrackCount = -1;
        }
#endif
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
