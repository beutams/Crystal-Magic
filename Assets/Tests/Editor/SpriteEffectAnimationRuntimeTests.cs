using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using UnityEngine;

public sealed class SpriteEffectAnimationRuntimeTests
{
    [TestCase(WorldFlags.Game)]
    [TestCase(WorldFlags.GameClient)]
    public void AllPhasesUseStablePathsWhenSceneAndResourceClipInstancesDiffer(WorldFlags flags)
    {
        using World world = new("Bundled sprite effect phases", flags);
        GameObject root = new("Effect resource fixture");
        root.AddComponent<ResourceComponent>();
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        UnitAnimationFrameLibrary library = ScriptableObject.CreateInstance<UnitAnimationFrameLibrary>();
        Texture2D texture = new(1, 1);
        Sprite[] sprites = new Sprite[3];
        AnimationClip[] resources = new AnimationClip[3];
        AnimationClip[] sceneClips = new AnimationClip[3];
        try
        {
            string[] paths = { "Assets/FX/Enter.anim", "Assets/FX/Loop.anim", "Assets/FX/Exit.anim" };
            for (int index = 0; index < 3; index++)
            {
                // Identical names must not select another phase's track.
                resources[index] = new AnimationClip { name = "Effect" };
                sceneClips[index] = new AnimationClip { name = "Effect" };
                sprites[index] = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
                library.Tracks.Add(new UnitAnimationFrameTrack
                {
                    ClipPath = paths[index], SourceClip = resources[index],
                    Sprites = new[] { sprites[index] }, SpriteTimes = new[] { 0f }, Length = 1f,
                });
            }
            EntityManager manager = world.EntityManager;
            Entity effect = manager.CreateEntity(typeof(DestroyEntityFlag));
            manager.SetComponentEnabled<DestroyEntityFlag>(effect, false);
            manager.AddComponentObject(effect, renderer);
            SpriteEffectAnimationComponent animation = new()
            {
                EnterClip = sceneClips[0], LoopClip = sceneClips[1], ExitClip = sceneClips[2],
                EnterClipPath = paths[0], LoopClipPath = paths[1], ExitClipPath = paths[2],
                RemainingLoopSeconds = -1f,
            };
            manager.AddComponentObject(effect, animation);
            SpriteEffectAnimationSystem system = world.GetOrCreateSystemManaged<SpriteEffectAnimationSystem>();
            typeof(SpriteEffectAnimationSystem).GetField("_frameLibrary", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(system, library);
            world.SetTime(new TimeData(0.1, 0.1f));
            system.Update();
            Assert.That(renderer.sprite, Is.SameAs(sprites[0]));
            Assert.That(manager.IsComponentEnabled<DestroyEntityFlag>(effect), Is.False);
            animation.PhaseElapsedSeconds = 1f;
            system.Update(); // Finish enter and start loop.
            system.Update();
            Assert.That(renderer.sprite, Is.SameAs(sprites[1]));
            SpriteEffectAnimationSystem.RequestEnd(manager, effect);
            system.Update();
            Assert.That(renderer.sprite, Is.SameAs(sprites[2]));
            animation.PhaseElapsedSeconds = 1f;
            system.Update();
            Assert.That(manager.IsComponentEnabled<DestroyEntityFlag>(effect), Is.True);
        }
        finally
        {
            foreach (Sprite sprite in sprites) Object.DestroyImmediate(sprite);
            foreach (AnimationClip clip in resources) Object.DestroyImmediate(clip);
            foreach (AnimationClip clip in sceneClips) Object.DestroyImmediate(clip);
            Object.DestroyImmediate(library);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(root);
        }
    }
}
