using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    public static class SkillProjectileRegistry
    {
        public sealed class State
        {
            public GameObject Visual;
            public SkillContent Context;
            public EffectData[] OnCollisionEffects;
            public EffectData[] OnDestroyEffects;
            public readonly HashSet<Entity> HitEntities = new();
        }

        private static readonly Dictionary<int, State> States = new();
        private static int _nextId = 1;

        public static int Register(GameObject visual, SkillContent context, EffectData[] onCollisionEffects, EffectData[] onDestroyEffects)
        {
            int id = _nextId++;
            States[id] = new State
            {
                Visual = visual,
                Context = context.Clone(),
                OnCollisionEffects = onCollisionEffects,
                OnDestroyEffects = onDestroyEffects,
            };

            return id;
        }

        public static bool TryGet(int id, out State state) => States.TryGetValue(id, out state);

        public static bool TryRemove(int id, out State state)
        {
            if (!States.TryGetValue(id, out state))
                return false;

            States.Remove(id);
            return true;
        }
    }
}
