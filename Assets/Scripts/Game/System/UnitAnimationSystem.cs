using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(GamePresentationSystemGroup))]
[UpdateAfter(typeof(StateScriptSystem))]
partial class UnitAnimationSystem : SystemBase
{
    private const string FrameLibraryPath = "Assets/Res/Data/UnitAnimationFrameLibrary.asset";

    private readonly HashSet<string> _missingResources = new(StringComparer.Ordinal);
    private DataTable<UnitAnimationProfileData> _cachedProfileTable;
    private UnitAnimationFrameLibrary _frameLibrary;
    private int _cachedProfileCount = -1;
    private int _cachedTrackCount = -1;

    private NativeParallelHashMap<UnitAnimationEntryKey, UnitAnimationDirectionMode> _directionModes;
    private NativeParallelHashMap<UnitAnimationTrackKey, int> _trackIds;
    private NativeArray<UnitAnimationTrackRuntime> _tracks;
    private NativeArray<float> _spriteTimes;
    private NativeArray<float> _flipTimes;
    private NativeArray<float> _flipValues;

    private EntityQuery _missingSampleQuery;
    private EntityQuery _playbackQuery;

    protected override void OnCreate()
    {
        _missingSampleQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<UnitAnimationComponent>() },
            None = new[] { ComponentType.ReadOnly<UnitAnimationSampleComponent>() },
        });
        _playbackQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadWrite<UnitAnimationComponent>(),
                ComponentType.ReadWrite<UnitAnimationSampleComponent>(),
                ComponentType.ReadOnly<UnitAoiActiveTag>(),
            },
            Options = EntityQueryOptions.IgnoreComponentEnabledState,
        });

        RequireForUpdate<UnitAnimationComponent>();
        RequireForUpdate<UnitAoiSingleton>();
    }

    protected override void OnUpdate()
    {
        if (ResourceComponent.Instance == null || DataComponent.Instance == null)
            return;

        DataTable<UnitAnimationProfileData> profileTable =
            DataComponent.Instance.GetTable<UnitAnimationProfileData>();
        if (profileTable == null || !TryLoadFrameLibrary())
            return;

        EnsureRuntimeComponents();
        EnsureRuntimeCache(profileTable);
        if (!_directionModes.IsCreated || !_trackIds.IsCreated || !_tracks.IsCreated)
            return;

        Dependency = new UnitAnimationPlaybackJob
        {
            DeltaTime = math.max(0f, SystemAPI.Time.DeltaTime),
            DirectionModes = _directionModes,
            TrackIds = _trackIds,
            Tracks = _tracks,
            SpriteTimes = _spriteTimes,
            FlipTimes = _flipTimes,
            FlipValues = _flipValues,
            StateScripts = GetComponentLookup<UnitStateScriptComponent>(true),
            Facings = GetComponentLookup<UnitFacingComponent>(true),
        }.ScheduleParallel(_playbackQuery, Dependency);

        // SpriteRenderer is managed. Complete only at the final presentation boundary;
        // all animation selection and sampling above still runs in Burst for every unit.
        Dependency.Complete();
        ApplyVisibleSamples();
    }

    protected override void OnDestroy()
    {
        Dependency.Complete();
        DisposeRuntimeCache();

        if (ResourceComponent.Instance != null && _frameLibrary != null)
            ResourceComponent.Instance.Unload(FrameLibraryPath);

        _frameLibrary = null;
        _cachedProfileTable = null;
        _cachedProfileCount = -1;
        _cachedTrackCount = -1;
        _missingResources.Clear();
        base.OnDestroy();
    }

    private bool TryLoadFrameLibrary()
    {
        if (_frameLibrary == null)
            _frameLibrary = ResourceComponent.Instance.Load<UnitAnimationFrameLibrary>(FrameLibraryPath);

        if (_frameLibrary != null)
            return true;

        if (_missingResources.Add(FrameLibraryPath))
            Debug.LogWarning($"[UnitAnimationSystem] Missing animation frame library: {FrameLibraryPath}");
        return false;
    }

    private void EnsureRuntimeComponents()
    {
        if (_missingSampleQuery.IsEmptyIgnoreFilter)
            return;

        Dependency.Complete();
        EntityManager.AddComponent<UnitAnimationSampleComponent>(_missingSampleQuery);
    }

    private void EnsureRuntimeCache(DataTable<UnitAnimationProfileData> profileTable)
    {
        int trackCount = _frameLibrary.Tracks?.Count ?? 0;
        if (ReferenceEquals(_cachedProfileTable, profileTable) &&
            _cachedProfileCount == profileTable.Count &&
            _cachedTrackCount == trackCount &&
            _directionModes.IsCreated &&
            _trackIds.IsCreated &&
            _tracks.IsCreated)
        {
            return;
        }

        Dependency.Complete();
        DisposeRuntimeCache();

        int entryCount = CountAnimationEntries(profileTable);
        _directionModes = new NativeParallelHashMap<UnitAnimationEntryKey, UnitAnimationDirectionMode>(
            math.max(1, entryCount), Allocator.Persistent);
        _trackIds = new NativeParallelHashMap<UnitAnimationTrackKey, int>(
            math.max(1, entryCount * 4), Allocator.Persistent);

        BuildTrackRuntimeData(trackCount);
        BuildEntryRuntimeData(profileTable);

        _cachedProfileTable = profileTable;
        _cachedProfileCount = profileTable.Count;
        _cachedTrackCount = trackCount;
    }

    private static int CountAnimationEntries(DataTable<UnitAnimationProfileData> profileTable)
    {
        int count = 0;
        foreach (UnitAnimationProfileData profile in profileTable.GetAll())
        {
            if (profile?.Animations != null)
                count += profile.Animations.Count;
        }

        return count;
    }

    private void BuildTrackRuntimeData(int trackCount)
    {
        int spriteTimeCount = 0;
        int flipTimeCount = 0;
        int flipValueCount = 0;
        for (int index = 0; index < trackCount; index++)
        {
            UnitAnimationFrameTrack track = _frameLibrary.Tracks[index];
            if (track == null)
                continue;

            spriteTimeCount += track.SpriteTimes?.Length ?? 0;
            flipTimeCount += track.FlipXTimes?.Length ?? 0;
            flipValueCount += track.FlipXValues?.Length ?? 0;
        }

        _tracks = new NativeArray<UnitAnimationTrackRuntime>(trackCount, Allocator.Persistent);
        _spriteTimes = new NativeArray<float>(spriteTimeCount, Allocator.Persistent);
        _flipTimes = new NativeArray<float>(flipTimeCount, Allocator.Persistent);
        _flipValues = new NativeArray<float>(flipValueCount, Allocator.Persistent);

        int spriteTimeOffset = 0;
        int flipTimeOffset = 0;
        int flipValueOffset = 0;
        for (int trackId = 0; trackId < trackCount; trackId++)
        {
            UnitAnimationFrameTrack track = _frameLibrary.Tracks[trackId];
            if (track == null)
            {
                _tracks[trackId] = UnitAnimationTrackRuntime.Invalid;
                continue;
            }

            float[] spriteTimes = track.SpriteTimes ?? Array.Empty<float>();
            float[] flipTimes = track.FlipXTimes ?? Array.Empty<float>();
            float[] flipValues = track.FlipXValues ?? Array.Empty<float>();
            CopyToNative(spriteTimes, _spriteTimes, spriteTimeOffset);
            CopyToNative(flipTimes, _flipTimes, flipTimeOffset);
            CopyToNative(flipValues, _flipValues, flipValueOffset);

            _tracks[trackId] = new UnitAnimationTrackRuntime
            {
                SpriteTimeOffset = spriteTimeOffset,
                SpriteTimeCount = spriteTimes.Length,
                SpriteCount = track.Sprites?.Length ?? 0,
                FlipTimeOffset = flipTimeOffset,
                FlipTimeCount = flipTimes.Length,
                FlipValueOffset = flipValueOffset,
                FlipValueCount = flipValues.Length,
                Length = math.max(0f, track.Length),
                IsLooping = track.IsLooping ? (byte)1 : (byte)0,
                IsValid = 1,
            };

            spriteTimeOffset += spriteTimes.Length;
            flipTimeOffset += flipTimes.Length;
            flipValueOffset += flipValues.Length;
        }
    }

    private void BuildEntryRuntimeData(DataTable<UnitAnimationProfileData> profileTable)
    {
        foreach (UnitAnimationProfileData profile in profileTable.GetAll())
        {
            if (profile == null || profile.UnitDataId < 0)
                continue;

            profile.Normalize();
            for (int index = 0; index < profile.Animations.Count; index++)
            {
                UnitAnimationEntryData entry = profile.Animations[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
                    continue;

                FixedString64Bytes animationName = default;
                if (animationName.CopyFrom(entry.Name) != CopyError.None)
                    continue;

                UnitAnimationEntryKey entryKey = new(profile.UnitDataId, animationName);
                _directionModes.TryAdd(entryKey, entry.DirectionMode);
                AddTrack(entryKey, UnitAnimationDirection.Front, entry.FrontClipPath);
                AddTrack(entryKey, UnitAnimationDirection.Back, entry.BackClipPath);
                AddTrack(entryKey, UnitAnimationDirection.Left, entry.LeftClipPath);
                AddTrack(entryKey, UnitAnimationDirection.Right, entry.RightClipPath);
            }
        }
    }

    private void AddTrack(
        in UnitAnimationEntryKey entryKey,
        UnitAnimationDirection direction,
        string clipPath)
    {
        int trackId = _frameLibrary.FindTrackIndex(clipPath);
        if (trackId < 0)
            return;

        _trackIds.TryAdd(new UnitAnimationTrackKey(entryKey, direction), trackId);
    }

    private static void CopyToNative(float[] source, NativeArray<float> destination, int offset)
    {
        for (int index = 0; index < source.Length; index++)
            destination[offset + index] = source[index];
    }

    private void ApplyVisibleSamples()
    {
        foreach ((RefRW<UnitAnimationSampleComponent> sampleRef, Entity entity) in
                 SystemAPI.Query<RefRW<UnitAnimationSampleComponent>>()
                     .WithAll<UnitAoiActiveTag>()
                     .WithEntityAccess())
        {
            UnitAnimationSampleComponent sample = sampleRef.ValueRO;
            if (sample.AppliedRevision == sample.Revision)
                continue;

            if (sample.TrackId < 0 || sample.SpriteIndex < 0)
            {
                sample.AppliedRevision = sample.Revision;
                sampleRef.ValueRW = sample;
                continue;
            }

            UnitAnimationFrameTrack track = _frameLibrary.GetTrack(sample.TrackId);
            if (track?.Sprites == null || sample.SpriteIndex >= track.Sprites.Length)
            {
                sample.AppliedRevision = sample.Revision;
                sampleRef.ValueRW = sample;
                continue;
            }

            if (!EntityManager.HasComponent<SpriteRenderer>(entity))
                continue;

            SpriteRenderer spriteRenderer = EntityManager.GetComponentObject<SpriteRenderer>(entity);
            spriteRenderer.sprite = track.Sprites[sample.SpriteIndex];
            if (sample.HasFlipX != 0)
                spriteRenderer.flipX = sample.FlipX != 0;

            sample.AppliedRevision = sample.Revision;
            sampleRef.ValueRW = sample;
        }
    }

    private void DisposeRuntimeCache()
    {
        if (_directionModes.IsCreated)
            _directionModes.Dispose();
        if (_trackIds.IsCreated)
            _trackIds.Dispose();
        if (_tracks.IsCreated)
            _tracks.Dispose();
        if (_spriteTimes.IsCreated)
            _spriteTimes.Dispose();
        if (_flipTimes.IsCreated)
            _flipTimes.Dispose();
        if (_flipValues.IsCreated)
            _flipValues.Dispose();
    }
}

[BurstCompile]
[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
public partial struct UnitAnimationPlaybackJob : IJobEntity
{
    public float DeltaTime;

    [ReadOnly]
    public NativeParallelHashMap<UnitAnimationEntryKey, UnitAnimationDirectionMode> DirectionModes;

    [ReadOnly]
    public NativeParallelHashMap<UnitAnimationTrackKey, int> TrackIds;

    [ReadOnly]
    public NativeArray<UnitAnimationTrackRuntime> Tracks;

    [ReadOnly]
    public NativeArray<float> SpriteTimes;

    [ReadOnly]
    public NativeArray<float> FlipTimes;

    [ReadOnly]
    public NativeArray<float> FlipValues;

    [ReadOnly]
    public ComponentLookup<UnitStateScriptComponent> StateScripts;

    [ReadOnly]
    public ComponentLookup<UnitFacingComponent> Facings;

    private void Execute(
        Entity entity,
        ref UnitAnimationComponent animation,
        ref UnitAnimationSampleComponent sample,
        EnabledRefRO<UnitAoiActiveTag> aoiActive)
    {
        if (animation.AnimationName.Length == 0)
        {
            ResetPlayback(ref animation);
            SetInvalidSample(ref sample);
            return;
        }

        bool requestChanged = !animation.PlayingAnimationName.Equals(animation.AnimationName) ||
                              animation.PlayingSequence != animation.Sequence;
        if (requestChanged)
        {
            animation.CurrentSampleTime = 0f;
            animation.CurrentClipLength = 0f;
        }

        if (!StateScripts.TryGetComponent(entity, out UnitStateScriptComponent stateScript) ||
            stateScript.UnitDataId < 0)
        {
            SetInvalidSample(ref sample);
            return;
        }

        UnitAnimationEntryKey entryKey = new(stateScript.UnitDataId, animation.AnimationName);
        if (!DirectionModes.TryGetValue(entryKey, out UnitAnimationDirectionMode directionMode))
        {
            SetInvalidSample(ref sample);
            return;
        }

        UnitAnimationDirection direction = ResolveDirection(entity, directionMode, ref animation);
        if (!TrackIds.TryGetValue(new UnitAnimationTrackKey(entryKey, direction), out int trackId) ||
            trackId < 0 || trackId >= Tracks.Length)
        {
            SetInvalidSample(ref sample);
            return;
        }

        UnitAnimationTrackRuntime track = Tracks[trackId];
        if (track.IsValid == 0)
        {
            SetInvalidSample(ref sample);
            return;
        }

        animation.CurrentClipLength = track.Length;
        if (requestChanged)
        {
            animation.PlayingAnimationName = animation.AnimationName;
            animation.PlayingSequence = animation.Sequence;
            animation.ElapsedSeconds = math.max(0f, animation.RequestedStartElapsedSeconds);
        }
        else
        {
            animation.ElapsedSeconds += DeltaTime;
        }

        float sampleTime = GetSampleTime(track.Length, track.IsLooping != 0, animation.ElapsedSeconds);
        animation.CurrentSampleTime = sampleTime;
        int spriteIndex = SampleIndex(
            SpriteTimes,
            track.SpriteTimeOffset,
            track.SpriteTimeCount,
            track.SpriteCount,
            sampleTime);

        byte hasFlipX = track.FlipValueCount > 0 ? (byte)1 : (byte)0;
        byte flipX = 0;
        if (hasFlipX != 0)
        {
            int flipIndex = SampleIndex(
                FlipTimes,
                track.FlipTimeOffset,
                track.FlipTimeCount,
                track.FlipValueCount,
                sampleTime);
            if (flipIndex >= 0 && FlipValues[track.FlipValueOffset + flipIndex] >= 0.5f)
                flipX = 1;
        }

        SetSample(ref sample, trackId, spriteIndex, hasFlipX, flipX);
    }

    private UnitAnimationDirection ResolveDirection(
        Entity entity,
        UnitAnimationDirectionMode directionMode,
        ref UnitAnimationComponent animation)
    {
        if (!Facings.TryGetComponent(entity, out UnitFacingComponent facing))
        {
            return directionMode == UnitAnimationDirectionMode.TwoDirections
                ? animation.LastTwoDirectionFacing
                : UnitAnimationDirection.Front;
        }

        float2 direction = math.normalizesafe(facing.Direction, new float2(1f, 0f));
        if (directionMode == UnitAnimationDirectionMode.TwoDirections)
        {
            if (direction.x < -0.0001f)
                animation.LastTwoDirectionFacing = UnitAnimationDirection.Left;
            else if (direction.x > 0.0001f)
                animation.LastTwoDirectionFacing = UnitAnimationDirection.Right;
            return animation.LastTwoDirectionFacing;
        }

        float bestDot = float.NegativeInfinity;
        UnitAnimationDirection bestDirection = UnitAnimationDirection.Front;
        EvaluateCardinal(direction, new float2(0f, -1f), UnitAnimationDirection.Front, ref bestDot, ref bestDirection);
        EvaluateCardinal(direction, new float2(0f, 1f), UnitAnimationDirection.Back, ref bestDot, ref bestDirection);
        EvaluateCardinal(direction, new float2(-1f, 0f), UnitAnimationDirection.Left, ref bestDot, ref bestDirection);
        EvaluateCardinal(direction, new float2(1f, 0f), UnitAnimationDirection.Right, ref bestDot, ref bestDirection);
        return bestDirection;
    }

    private static void EvaluateCardinal(
        float2 direction,
        float2 cardinal,
        UnitAnimationDirection candidate,
        ref float bestDot,
        ref UnitAnimationDirection bestDirection)
    {
        float dot = math.dot(direction, cardinal);
        if (dot <= bestDot)
            return;

        bestDot = dot;
        bestDirection = candidate;
    }

    private static float GetSampleTime(float length, bool isLooping, float elapsedSeconds)
    {
        if (length <= 0f)
            return 0f;

        float elapsed = math.max(0f, elapsedSeconds);
        return isLooping ? elapsed % length : math.min(elapsed, length);
    }

    private static int SampleIndex(
        NativeArray<float> times,
        int timeOffset,
        int timeCount,
        int valueCount,
        float time)
    {
        if (valueCount <= 0)
            return -1;

        int count = math.min(timeCount, valueCount);
        if (count <= 1)
            return 0;

        int low = 1;
        int high = count;
        while (low < high)
        {
            int middle = (low + high) >> 1;
            if (times[timeOffset + middle] <= time)
                low = middle + 1;
            else
                high = middle;
        }

        return low - 1;
    }

    private static void SetSample(
        ref UnitAnimationSampleComponent sample,
        int trackId,
        int spriteIndex,
        byte hasFlipX,
        byte flipX)
    {
        if (sample.Initialized != 0 &&
            sample.TrackId == trackId &&
            sample.SpriteIndex == spriteIndex &&
            sample.HasFlipX == hasFlipX &&
            sample.FlipX == flipX)
        {
            return;
        }

        sample.TrackId = trackId;
        sample.SpriteIndex = spriteIndex;
        sample.HasFlipX = hasFlipX;
        sample.FlipX = flipX;
        sample.Initialized = 1;
        sample.Revision = NextRevision(sample.Revision);
    }

    private static void SetInvalidSample(ref UnitAnimationSampleComponent sample)
    {
        SetSample(ref sample, -1, -1, 0, 0);
    }

    private static uint NextRevision(uint revision)
    {
        revision++;
        return revision == 0u ? 1u : revision;
    }

    private static void ResetPlayback(ref UnitAnimationComponent animation)
    {
        animation.PlayingAnimationName = default;
        animation.PlayingSequence = 0u;
        animation.ElapsedSeconds = 0f;
        animation.CurrentSampleTime = 0f;
        animation.CurrentClipLength = 0f;
    }
}

public readonly struct UnitAnimationEntryKey : IEquatable<UnitAnimationEntryKey>
{
    public UnitAnimationEntryKey(int unitDataId, in FixedString64Bytes animationName)
    {
        UnitDataId = unitDataId;
        AnimationName = animationName;
    }

    public int UnitDataId { get; }
    public FixedString64Bytes AnimationName { get; }

    public bool Equals(UnitAnimationEntryKey other)
    {
        return UnitDataId == other.UnitDataId && AnimationName.Equals(other.AnimationName);
    }

    public override bool Equals(object obj)
    {
        return obj is UnitAnimationEntryKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (UnitDataId * 397) ^ AnimationName.GetHashCode();
        }
    }
}

public readonly struct UnitAnimationTrackKey : IEquatable<UnitAnimationTrackKey>
{
    public UnitAnimationTrackKey(
        in UnitAnimationEntryKey entry,
        UnitAnimationDirection direction)
    {
        Entry = entry;
        Direction = direction;
    }

    public UnitAnimationEntryKey Entry { get; }
    public UnitAnimationDirection Direction { get; }

    public bool Equals(UnitAnimationTrackKey other)
    {
        return Entry.Equals(other.Entry) && Direction == other.Direction;
    }

    public override bool Equals(object obj)
    {
        return obj is UnitAnimationTrackKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Entry.GetHashCode() * 397) ^ (int)Direction;
        }
    }
}

public struct UnitAnimationTrackRuntime
{
    public int SpriteTimeOffset;
    public int SpriteTimeCount;
    public int SpriteCount;
    public int FlipTimeOffset;
    public int FlipTimeCount;
    public int FlipValueOffset;
    public int FlipValueCount;
    public float Length;
    public byte IsLooping;
    public byte IsValid;

    public static UnitAnimationTrackRuntime Invalid => new()
    {
        SpriteTimeOffset = 0,
        SpriteTimeCount = 0,
        SpriteCount = 0,
        FlipTimeOffset = 0,
        FlipTimeCount = 0,
        FlipValueOffset = 0,
        FlipValueCount = 0,
        Length = 0f,
        IsLooping = 0,
        IsValid = 0,
    };
}
