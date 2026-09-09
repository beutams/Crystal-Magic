# Sprite Effect Lifecycle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace grid-texture Quad VFX with ECS-sampled AnimationClip/SpriteRenderer visuals and configure Fireball's projectile and impact effects through that system.

**Architecture:** Animation clips live on visual prefabs through `SpriteEffectAnimationAuthoring`. The Baker records managed clip/runtime state and `SpriteEffectAnimationSystem` samples the existing shared frame library into SpriteRenderer companions. Gameplay projectiles are separate ECS entities; their visuals are independent VFX entities that follow and finish after the logic owner is gone.

**Tech Stack:** Unity 6, Unity Entities, Unity `AnimationClip` sprite curves, SpriteRenderer companions, Newtonsoft JSON data tables, Unity Editor AssetDatabase.

**Spec:** `Docs/superpowers/specs/2026-08-29-sprite-effect-lifecycle-design.md`

## Global Constraints

- Use `AnimationClip` only as a parsed frame source; do not add Animator or AnimatorController components.
- Visual prefabs use a SpriteRenderer companion and `SpriteEffectAnimationAuthoring`; Bakers must not add SpriteRenderer themselves.
- Projectile collision, damage and destruction occur immediately and independently from visual exit playback.
- Preserve existing Fireball combat parameters and store JSON as UTF-8 BOM.
- The shared working tree contains user changes; do not stage or commit implementation files. Stage and commit only the dedicated design document.
- Keep `cs_review_status.txt` aligned with reviewed C# changes.

---

### Task 1: Add generic sprite-effect playback and frame-library collection

**Files:**
- Create: `Assets/Scripts/Game/Unit/Component/SpriteEffectAnimationAuthoring.cs`
- Create: `Assets/Scripts/Game/Unit/System/SpriteEffectAnimationSystem.cs`
- Modify: `Assets/Scripts/Game/Data/UnitAnimationFrameLibrary.cs`
- Modify: `Assets/Scripts/Game/Data/Editor/UnitAnimationFrameLibraryBuilder.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/QuadAnimationComponent.cs`
- Modify: `Assets/Scripts/Game/Unit/System/QuadOverlayPulseSystem.cs`
- Test: Unity Editor frame-library rebuild and compile verification

**Interfaces:**
- Produces `SpriteEffectAnimationComponent`, `SpriteEffectAnimationPhase`, and `EffectVisualFollowComponent` for effect spawners and projectile spawning.
- Produces `SpriteEffectAnimationSystem.RequestEnd(EntityManager, Entity)` for lifecycle owners to request final playback.
- Extends `UnitAnimationFrameLibraryBuilder.Rebuild(IEnumerable<UnitAnimationProfileData>)` so its library includes clips from visual prefabs.
- Produces `UnitAnimationFrameLibrary.Find(AnimationClip clip)` so runtime effect playback performs no AssetDatabase lookup.

- [ ] **Step 1: Add the failing frame-library collection check**

In the editor, create a temporary prefab with `SpriteEffectAnimationAuthoring` referencing a sprite AnimationClip. Run `UnitAnimationFrameLibraryBuilder.RebuildFromSavedProfiles()` and inspect `Assets/Res/Data/UnitAnimationFrameLibrary.asset`. The clip path must be absent before the builder is extended.

- [ ] **Step 2: Implement authoring and state**

Create `SpriteEffectAnimationAuthoring.cs` with a root-only authoring class and a nested Baker that calls:

```csharp
AddComponentObject(entity, new SpriteEffectAnimationComponent
{
    EnterClip = authoring.EnterClip,
    LoopClip = authoring.LoopClip,
    ExitClip = authoring.ExitClip,
    Phase = SpriteEffectAnimationPhase.Uninitialized,
    RemainingLoopSeconds = -1f,
});
```

Define `EffectVisualFollowComponent : IComponentData` with `Entity Target`, `float3 Offset`, `byte AlignRotation`, and `byte EndWhenTargetMissing`. Keep clip, renderer, phase, sample time, elapsed time, current sprite, and end-request fields in the managed animation component.

- [ ] **Step 3: Implement playback and follow termination**

Add `UnitAnimationFrameLibrary.Find(AnimationClip clip)` by comparing `track.SourceClip` references. Create `SpriteEffectAnimationSystem.cs` as `SystemBase` in `UnitExecutionSystemGroup`, before `DestroyEntitySystem`. Resolve `SpriteRenderer` with `EntityManager.GetComponentObject<SpriteRenderer>(entity)`, sample `UnitAnimationFrameLibrary.Find(currentClip)` tracks, and set the renderer sprite/flip. Implement this phase transition:

```csharp
Enter -> first available of Loop/Exit -> Exit -> Destroy
Loop + EndRequested -> Exit or Destroy
```

When `EffectVisualFollowComponent.Target` no longer exists, leave the current transform unchanged, set `EndRequested`, and progress the visual independently.

- [ ] **Step 4: Extend the frame library builder**

Modify `UnitAnimationFrameLibraryBuilder` to scan all prefabs under `Assets/Res/Prefab/Projectile` and `Assets/Res/Prefab/VFX`, load `SpriteEffectAnimationAuthoring`, and add the asset paths of non-null Enter/Loop/Exit clips to its path set. Treat prefab imports in those folders as a scheduled rebuild trigger. Keep unit profile scanning unchanged.

- [ ] **Step 5: Remove Quad animation dependency while preserving hit overlays**

Move `QuadOverlayPulseComponent` into a focused retained component file. Remove `QuadAnimationComponent`, `QuadAnimationVisualComponent`, frame-UV properties, and `FollowEntityComponent`; delete `QuadAnimationSystem.cs`; change `QuadOverlayPulseSystem` to no longer declare `UpdateAfter(typeof(QuadAnimationSystem))`.

- [ ] **Step 6: Verify Task 1**

Run:

```powershell
dotnet build 'Assembly-CSharp-Editor.csproj' -nologo --no-restore -v:minimal
```

Expected: zero C# errors. In Unity, rebuild the frame library and verify each temporary visual Clip appears as a track; remove the temporary test prefab afterwards.

### Task 2: Add static/follow sprite effects and projectile visual spawning

**Files:**
- Modify: `Assets/Scripts/Game/Data/Effects/SpawnProjectileEffectData.cs`
- Modify: `Assets/Scripts/Game/Data/Effects/SpawnVfxEffectData.cs`
- Create: `Assets/Scripts/Game/Data/Effects/SpawnFollowVfxEffectData.cs`
- Modify: `Assets/Scripts/Game/Skill/Effects/SpawnProjectileEffect.cs`
- Modify: `Assets/Scripts/Game/Skill/Effects/SpawnVfxEffect.cs`
- Create: `Assets/Scripts/Game/Skill/Effects/SpawnFollowVfxEffect.cs`
- Create: `Assets/Scripts/Game/Skill/Effects/SpriteEffectSpawnUtility.cs`
- Modify: `Assets/Scripts/Game/Skill/SkillExecutor.cs`
- Modify: `Assets/Scripts/Game/Unit/Component/SkillProjectileRuntime.cs`
- Modify: `Assets/Scripts/Game/Unit/System/SkillProjectileSpawnSystem.cs`
- Test: Fireball and direct static/follow visual spawn in the training scene

**Interfaces:**
- `SpawnProjectileEffectData.VisualPrefabName` travels through `SkillProjectileSpawnRequest` to `SkillProjectileSpawnSystem`.
- `SpawnVfxEffectData` spawns a static visual through `SpriteEffectSpawnUtility.SpawnStatic`.
- `SpawnFollowVfxEffectData` spawns a caster/target visual through `SpriteEffectSpawnUtility.SpawnFollow`.
- `SpriteEffectSpawnUtility` writes `LocalTransform`, `SpriteEffectAnimationComponent.RemainingLoopSeconds`, and optional `EffectVisualFollowComponent`.

- [ ] **Step 1: Define visual-effect data fields**

Add a `VisualPrefabName` string to `SpawnProjectileEffectData`. Replace `SpawnVfxEffectData` texture-grid fields with `VisualPrefabName`, `DurationSeconds`, `Scale`, `SpawnOffset`, and `AlignToCasterForward`. Add `SpawnFollowVfxEffectData` with those transform fields plus:

```csharp
public EffectVisualFollowTarget FollowTarget = EffectVisualFollowTarget.Caster;
public bool AlignToFollowTargetForward;
public float DurationSeconds = -1f;
```

Keep `CreateRuntimeCopy` applying `EffectDuration` and `VfxScale` to duration/scale.

- [ ] **Step 2: Implement a single visual-spawn utility**

Create `SpriteEffectSpawnUtility` that resolves a VFX prefab through `EntitySpawnRegistryUtility.TryInstantiateVfx`, sets `LocalTransform`, obtains `SpriteEffectAnimationComponent`, sets `RemainingLoopSeconds`, and attaches `EffectVisualFollowComponent` only for follow calls. Return `Entity.Null` and log one useful warning when a configured VFX prefab name is missing.

- [ ] **Step 3: Replace the static Quad spawn effect and add follow effect**

Rewrite `SpawnVfxEffect.Execute` to calculate the existing context position/rotation, then call `SpawnStatic`. Implement `SpawnFollowVfxEffect.Execute` using caster or target selection and `SpawnFollow`. Register `SpawnFollowVfxEffectData` in `SkillExecutor.CreateEffect` and the Effect Graph type registry.

- [ ] **Step 4: Spawn projectile visuals independently**

Add `VisualPrefabName` to `SkillProjectileSpawnRequest` and `SkillProjectilePayloadComponent`. Populate it in `SpawnProjectileEffect`. In `SkillProjectileSpawnSystem.SpawnProjectile`, call `SpriteEffectSpawnUtility.SpawnFollow` after creating the gameplay projectile, with the projectile entity as target and `EndWhenTargetMissing = 1`. Do not add a renderer or animation component to the gameplay projectile itself.

- [ ] **Step 5: Verify Task 2**

Run the compile command from Task 1. In `TrainingSubScene`, create one static visual, one caster-follow visual, and one projectile visual. Verify a missing follow target requests visual termination but does not delay or recreate the gameplay entity.

### Task 3: Create Fireball assets, migrate data, and remove generic Quad assets

**Files:**
- Create: `Assets/Res/Animation/Effects/FireballProjectileLoop.anim`
- Create: `Assets/Res/Animation/Effects/FireballSolarFlashEnter.anim`
- Create: `Assets/Res/Prefab/VFX/FireballProjectileVisual.prefab`
- Create: `Assets/Res/Prefab/VFX/FireballSolarFlash.prefab`
- Modify: `Assets/Res/Prefab/Projectile/Projectile.prefab`
- Delete: `Assets/Res/Prefab/VFX/VFX.prefab` and its `.meta`
- Modify: `Assets/Res/Data/SkillDataTable.json`
- Modify: `Assets/Scripts/Game/Data/Editor/UnitAnimationFrameLibraryBuilder.cs`
- Test: Fireball in `Assets/Scenes/SubScene/TrainingSubScene.unity`

**Interfaces:**
- The projectile logic prefab remains named `Projectile` and contains `SkillProjectileAuthoring` but no visible MeshRenderer/SpriteRenderer.
- `FireballProjectileVisual` has only a SpriteRenderer, PoolPreset, and `SpriteEffectAnimationAuthoring(LoopClip = FireballProjectileLoop)`.
- `FireballSolarFlash` has only a SpriteRenderer, PoolPreset, and `SpriteEffectAnimationAuthoring(EnterClip = FireballSolarFlashEnter)`.

- [ ] **Step 1: Generate deterministic sprite AnimationClip assets**

Add an editor asset-generation utility that loads all sprites from each sheet, orders them by sprite rect from left to right, writes sprite keyframes at 16 FPS to `m_Sprite`, sets `FireballProjectileLoop` looping, and sets `FireballSolarFlashEnter` non-looping. Invoke it once to create the two assets, then keep the generator as the reproducible editor tool.

- [ ] **Step 2: Create visual prefabs and strip the logic renderer**

Create the two VFX prefabs with root SpriteRenderers, PoolPreset, and the new authoring. Assign the generated clips and initial sprite frame. Remove MeshFilter/MeshRenderer and the FireBall material dependency from `Projectile.prefab`, retaining its transform, SkillProjectileAuthoring, and PoolPreset.

- [ ] **Step 3: Migrate the Fireball JSON**

Set Fireball's root projectile `VisualPrefabName` to `FireballProjectileVisual`. Replace the old texture-grid impact node with a static VFX node whose `VisualPrefabName` is `FireballSolarFlash`; retain the nested area search, enemy condition, radius `5`, fire element, and damage coefficient `2`. Write with UTF-8 BOM.

- [ ] **Step 4: Rebuild visual frame tracks and remove generic VFX prefab**

Use the frame-library builder to rebuild `Assets/Res/Data/UnitAnimationFrameLibrary.asset`, confirm both new clips are tracked, then delete the obsolete generic VFX prefab. Confirm automatic VFX prefab registry scanning finds the two named Fireball prefabs.

- [ ] **Step 5: Verify Task 3**

Open `TrainingSubScene`, cast Fireball at a Straw unit, and verify: the projectile visual loops while travelling; the gameplay projectile collides/damages once and disappears immediately; Solar Flash appears at impact, plays once, and self-cleans. Run:

```powershell
dotnet build 'Assembly-CSharp-Editor.csproj' -nologo --no-restore -v:minimal
```

Expected: zero warnings and zero errors.

### Task 4: Final cleanup and review tracking

**Files:**
- Modify: `cs_review_status.txt`
- Delete: `Assets/Scripts/Game/Unit/System/QuadAnimationSystem.cs.meta`
- Delete: obsolete Quad animation asset metadata if it has no remaining owner
- Test: reference search and final compile

**Interfaces:**
- No runtime or data file refers to `QuadAnimationComponent`, `QuadAnimationVisualComponent`, `FollowEntityComponent`, texture-grid VFX fields, or `Assets/Res/Prefab/VFX/VFX.prefab`.

- [ ] **Step 1: Run a stale-reference search**

Run:

```powershell
rg -n 'QuadAnimationComponent|QuadAnimationVisualComponent|FollowEntityComponent|VfxTexture|GridColumns|GridRows|FrameCount|FramesPerSecond' Assets/Scripts Assets/Res/Data
```

Expected: no results other than intentional unrelated material/shader fields outside the removed effect runtime.

- [ ] **Step 2: Delete only confirmed obsolete assets and metadata**

Remove the old Quad animation system and generic VFX prefab only after the reference search is clean. Retain the Quad overlay pulse component/system because hit overlays still depend on it.

- [ ] **Step 3: Update review state**

Mark newly reviewed C# files `TRUE`; append `DIRTY` to already-reviewed files that were modified. Do not alter entries for unrelated dirty files.

- [ ] **Step 4: Final verification**

Run the compile command from Task 1, validate `SkillDataTable.json` parses with PowerShell `ConvertFrom-Json`, and verify its first three bytes are the UTF-8 BOM sequence `EF BB BF`.
