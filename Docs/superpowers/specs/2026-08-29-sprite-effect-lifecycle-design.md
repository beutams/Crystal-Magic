# Sprite Effect Lifecycle Design

## Goal

Replace the texture-grid `QuadAnimation` VFX path with a reusable ECS sprite-effect lifecycle. Unity `AnimationClip` assets remain the authored frame source, while ECS samples their sprite curves and writes the result to `SpriteRenderer.sprite`; no effect prefab uses an `Animator` component at runtime.

## Scope

The feature provides three visual behaviours:

1. Projectile-owned visuals that follow a separate projectile-logic entity.
2. Static effects spawned once at the current skill-effect position.
3. Follow effects spawned once and continuously attached to the caster or target entity.

It includes Fireball as the first migrated content:

- `proj_fireball_sheet` is the projectile visual's looping clip.
- `fire_solarflash_sheet` is the impact visual's Enter clip and plays exactly once.

## Non-goals

- No `Animator`, `AnimatorController`, Animation Event, or `AnimationClip.SampleAnimation` runtime playback.
- No changes to projectile hit detection, damage formulae, target filtering, or effect-graph storage format beyond the new visual effect data nodes.
- No compatibility path for the old texture-grid VFX runtime after the Fireball data is migrated; unused Quad animation code and generic VFX prefab are removed.

## Authored Prefab Contract

Every visual prefab has a root `SpriteRenderer` and `SpriteEffectAnimationAuthoring`.

`SpriteEffectAnimationAuthoring` exposes three optional references:

| Field | Playback |
| --- | --- |
| `EnterClip` | Plays once at spawn, then advances. |
| `LoopClip` | Loops while the effect remains active. |
| `ExitClip` | Plays once after an end request, then destroys the visual entity. |

At spawn, the runtime selects the first available stage in `Enter`, `Loop`, `Exit` order. Missing stages are skipped. A non-looping stage advances immediately to the next available stage. A Loop stage remains active until a duration ends or the owner/follow target disappears. A visual with only an Enter clip therefore plays it once and destroys itself.

The Baker adds the managed animation runtime component only. It never adds a `SpriteRenderer`, avoiding the duplicate SpriteRenderer baking issue encountered by the unit animation chain. The system resolves the baked SpriteRenderer companion through `EntityManager.GetComponentObject<SpriteRenderer>`.

## Runtime Components And System

`SpriteEffectAnimationComponent` is managed because it retains `AnimationClip` and `SpriteRenderer` references. It contains the authored clips plus observable runtime state: current phase, current clip, elapsed phase time, current sample time, current sprite, and end-request state.

`EffectVisualFollowComponent` is an unmanaged component that stores the logic owner/target, local offset, rotation-alignment flag, and `EndWhenTargetMissing` policy.

`SpriteEffectAnimationSystem` runs in `UnitExecutionSystemGroup` before `DestroyEntitySystem` and:

1. Tracks the owner's `LocalTransform` while the owner exists.
2. Requests the visual end state at the last copied transform when its owner disappears.
3. Samples the current frame track and assigns `SpriteRenderer.sprite` and optional clip-authored `flipX`.
4. Advances Enter and Exit exactly once; wraps only Loop.
5. Sets `DestroyEntityFlag` only after the final stage completes.

The existing frame library remains the shared source of parsed frames. Its builder is extended to collect the clips referenced by `SpriteEffectAnimationAuthoring` components on projectile and VFX prefabs, in addition to unit animation profile clips.

## Logic And Visual Separation

The gameplay projectile entity remains named `Projectile` and owns only motion, collision, hit history, payload, and immediate destruction. It has no renderer.

`SpawnProjectileEffectData` gains `VisualPrefabName`. `SkillProjectileSpawnSystem` creates the gameplay projectile, then creates an independent VFX-registry entity for that visual prefab and attaches it using `EffectVisualFollowComponent`.

When a projectile hits or reaches its range, the gameplay projectile executes collision/destroy effects and receives `DestroyEntityFlag` immediately. Its visual sees its follow owner disappear, freezes at its last copied position, optionally plays Exit, and then destroys itself. A visual can therefore fade out without delaying damage, collision, or projectile pooling.

## Effect Data Nodes

The existing `SpawnVfxEffectData` becomes the static sprite-effect node. It selects `VisualPrefabName`, scale, offset, rotation alignment, and finite Loop duration. `SpawnFollowVfxEffectData` is added for caster/target-attached visuals. It selects `VisualPrefabName`, `FollowTarget` (`Caster` or `Target`), scale, offset, rotation alignment, and a finite or indefinite Loop duration.

Both nodes instantiate a prefab registered under `Assets/Res/Prefab/VFX`, configure its transform and runtime duration, and rely on `SpriteEffectAnimationSystem` for playback and cleanup.

## Fireball Content

`FireballProjectileVisual.prefab` is a VFX visual prefab with `proj_fireball_sheet` in `LoopClip`. `FireballSolarFlash.prefab` is a static VFX visual prefab with `fire_solarflash_sheet` in `EnterClip`; its Loop and Exit clips are empty.

Fireball's data chain is:

```text
Spawn Projectile (VisualPrefabName = FireballProjectileVisual)
  └─ OnDestroy
     ├─ Spawn Static VFX (VisualPrefabName = FireballSolarFlash)
     └─ Area Search → Fire Damage
```

The existing speed, range, collision radius, area radius, enemy filtering, and 2x fire damage are preserved.

## Verification

- The editor frame-library asset contains both generated Fireball clips.
- A Fireball projectile moves and loops its visual; its collision/damage entity disappears on the hit frame.
- The independent projectile visual is removed after its optional exit rather than participating in another collision.
- Solar Flash starts at the impact point, runs its Enter clip exactly once, and cleans itself up.
- Static and follow visual nodes work without any `QuadAnimationComponent` instances.
- `Assembly-CSharp-Editor.csproj` builds with `--no-restore` and zero C# errors.
