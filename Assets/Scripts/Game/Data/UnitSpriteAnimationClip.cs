using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CrystalMagic.Game.Data
{
    [CreateAssetMenu(fileName = "UnitSpriteAnimationClip", menuName = "Crystal Magic/Unit Sprite Animation Clip")]
    public sealed class UnitSpriteAnimationClip : ScriptableObject
    {
        [SerializeField] private float _framesPerSecond = 12f;
        [SerializeField] private bool _loop = true;
        [SerializeField] private Vector2 _referenceFrameSizePixels = new(48f, 48f);
        [SerializeField] private Vector2 _referenceFrameWorldSize = Vector2.one;
        [SerializeField] private Sprite[] _frontFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] _backFrames = Array.Empty<Sprite>();
        [FormerlySerializedAs("_leftFrames")]
        [SerializeField] private Sprite[] _rightFrames = Array.Empty<Sprite>();

        public float FramesPerSecond => Mathf.Max(0.01f, _framesPerSecond);
        public bool Loop => _loop;
        public Vector2 ReferenceFrameSizePixels => new(
            Mathf.Max(1f, _referenceFrameSizePixels.x),
            Mathf.Max(1f, _referenceFrameSizePixels.y));
        public Vector2 ReferenceFrameWorldSize => new(
            Mathf.Max(0.0001f, _referenceFrameWorldSize.x),
            Mathf.Max(0.0001f, _referenceFrameWorldSize.y));

        public bool TryGetFrame(
            UnitAnimationDirection direction,
            float elapsedSeconds,
            out Sprite sprite,
            out int frameIndex,
            out bool mirrorX)
        {
            Sprite[] frames = ResolveFrames(direction, out mirrorX);
            if (frames == null || frames.Length == 0)
            {
                sprite = null;
                frameIndex = -1;
                return false;
            }

            int rawIndex = Mathf.FloorToInt(Mathf.Max(0f, elapsedSeconds) * FramesPerSecond);
            frameIndex = _loop
                ? rawIndex % frames.Length
                : Mathf.Clamp(rawIndex, 0, frames.Length - 1);
            sprite = frames[frameIndex];
            return sprite != null;
        }

        public bool IsFinished(UnitAnimationDirection direction, float elapsedSeconds)
        {
            if (_loop)
                return false;

            Sprite[] frames = ResolveFrames(direction, out _);
            return frames != null &&
                   frames.Length > 0 &&
                   elapsedSeconds >= frames.Length / FramesPerSecond;
        }

        private Sprite[] ResolveFrames(UnitAnimationDirection direction, out bool mirrorX)
        {
            mirrorX = false;
            switch (direction)
            {
                case UnitAnimationDirection.Back when HasFrames(_backFrames):
                    return _backFrames;

                case UnitAnimationDirection.Right when HasFrames(_rightFrames):
                    return _rightFrames;

                case UnitAnimationDirection.Left when HasFrames(_rightFrames):
                    mirrorX = true;
                    return _rightFrames;

                case UnitAnimationDirection.Front when HasFrames(_frontFrames):
                    return _frontFrames;
            }

            if (HasFrames(_frontFrames))
                return _frontFrames;
            if (HasFrames(_rightFrames))
                return _rightFrames;
            return _backFrames;
        }

        private static bool HasFrames(Sprite[] frames)
        {
            return frames != null && frames.Length > 0;
        }

        private void OnValidate()
        {
            _framesPerSecond = Mathf.Max(0.01f, _framesPerSecond);
            _referenceFrameSizePixels.x = Mathf.Max(1f, _referenceFrameSizePixels.x);
            _referenceFrameSizePixels.y = Mathf.Max(1f, _referenceFrameSizePixels.y);
            _referenceFrameWorldSize.x = Mathf.Max(0.0001f, _referenceFrameWorldSize.x);
            _referenceFrameWorldSize.y = Mathf.Max(0.0001f, _referenceFrameWorldSize.y);
            _frontFrames ??= Array.Empty<Sprite>();
            _backFrames ??= Array.Empty<Sprite>();
            _rightFrames ??= Array.Empty<Sprite>();
        }
    }
}
