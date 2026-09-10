# Batch Character Prefabs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create complete, shadow-sprite-backed Unity prefabs, two-direction animation assets, preview controllers, and runtime data for the 18 remaining non-Wizard character folders.

**Architecture:** Each character uses its `with shadows` 100x100 animation strips as the single source of sprites. Every source action becomes matching left/right clips; left uses the same frame order as right and `m_FlipX: 1`. Each prefab is a direct clone of `MonsterStraw.prefab`, with the runtime animator disabled but a fully populated controller retained for Inspector preview. JSON data and the shared frame library connect the generated clips to runtime playback.

**Tech Stack:** Unity text-serialized assets (`.prefab`, `.anim`, `.controller`, `.meta`, `.asset`), UTF-8-with-BOM JSON, PowerShell read/validation commands, Unity AssetDatabase import.

**Spec:** User request in this Codex conversation, 2026-09-09.

## Global Constraints

- Generate exactly these 18 characters: Archer, Armored Axeman, Armored Orc, Armored Skeleton, Bat, Elite Orc, Greatsword Skeleton, Knight, Knight Templar, Lancer, Necromancer, Orc, Orc rider, Priest, Skeleton, Skeleton Archer, Slime, Soldier.
- Do not create or modify Wizard assets; Wizard is the player character.
- Do not use spell-only source images: any source name containing `Effect` or `(With magic effects)` is excluded. Plain character-body strips such as `Necromancer_Attack02.png`, `Necromancer_Summon.png`, `Priest_Attack.png`, and `Priest_Heal.png` remain included.
- Do not use each character's unsuffixed `Character.png` sprite atlas.
- Slice every selected source PNG into 100x100 sprites named `<Character>_<Action>_<index>`.
- Generate 127 source actions, 829 sprite frames, 254 left/right clips, and 18 controllers/prefabs.
- Copy `Assets/Res/Prefab/Unit/MonsterStraw.prefab`; preserve its components and disabled Animator. Set SpriteRenderer to the first Idle frame, except Bat which uses the first Flying frame.
- Left/right follow the existing Werewolf/Werebear composition: identical sprite sequence, `FlipXValues: 0` for right and `1` for left.
- JSON files `UnitDataTable.json`, `UnitAnimationProfileDataTable.json`, and `StateScriptDataTable.json` must be written with a UTF-8 BOM.
- Do not leave helper `.ps1` or `.cs` files in the workspace. Do not commit: the shared worktree already contains user-owned changes.

## Character Manifest

| Character | Actions (frame count) | Default |
|---|---|---|
| Archer | Idle6, Walk8, Attack01 9, Attack02 12, Hurt4, Death4 | Idle |
| Armored Axeman | Idle6, Walk8, Attack01 9, Attack02 9, Attack03 12, Hurt4, Death4 | Idle |
| Armored Orc | Idle6, Walk8, Attack01 7, Attack02 8, Attack03 9, Block4, Hurt4, Death4 | Idle |
| Armored Skeleton | Idle6, Walk8, Attack01 8, Attack02 9, Summon5, Hurt4, Death4 | Idle |
| Bat | Flying6, Attack01 6, Attack02 7, Hurt4, Death4 | Flying |
| Elite Orc | Idle6, Walk8, Attack01 7, Attack02 11, Attack03 9, Hurt4, Death4 | Idle |
| Greatsword Skeleton | Idle6, Walk9, Attack01 9, Attack02 12, Attack03 8, Summon5, Hurt4, Death4 | Idle |
| Knight | Idle6, Walk8, Attack01 7, Attack02 10, Attack03 11, Block4, Hurt4, Death4 | Idle |
| Knight Templar | Idle6, Walk01 8, Walk02 8, Attack01 7, Attack02 8, Attack03 11, Block4, Hurt4, Death4 | Idle |
| Lancer | Idle6, Walk01 8, Walk02 8, Attack01 6, Attack02 9, Attack03 8, Hurt4, Death4 | Idle |
| Necromancer | Idle6, Walk6, Attack01 9, Attack02 10, Summon10, Hurt4, `DEATH`9 (normalized to Death) | Idle |
| Orc | Idle6, Walk8, Attack01 6, Attack02 6, Hurt4, Death4 | Idle |
| Orc rider | Idle6, Walk8, Attack01 8, Attack02 9, Attack03 11, Block4, Hurt4, Death4 | Idle |
| Priest | Idle6, Walk8, Attack9, Heal6, Hurt4, Death4 | Idle |
| Skeleton | Idle6, Walk8, Attack01 6, Attack02 7, Block4, Summon5, Hurt4, Death4 | Idle |
| Skeleton Archer | Idle6, Walk8, Attack9, Summon5, Hurt4, Death4 | Idle |
| Slime | Idle6, Walk6, Attack01 6, Attack02 12, Hurt4, Death4 | Idle |
| Soldier | Idle6, Walk8, Attack01 6, Attack02 6, Attack03 9, Hurt4, Death4 | Idle |

---

### Task 1: Preflight the source manifest and ID ranges

**Files:**
- Read: `Assets/Res/Sprites/Units/Characters(100x100 split)/*/* with shadows/*.png`
- Read: `Assets/Res/Data/UnitDataTable.json`
- Read: `Assets/Res/Data/UnitAnimationProfileDataTable.json`
- Read: `Assets/Res/Data/StateScriptDataTable.json`

**Interfaces:**
- Consumes: Character Manifest in this plan.
- Produces: Validated action/frame map; confirmed allocation ranges UnitData 12–29, StateScript 11–28, and AnimationProfile 6–23.

- [ ] **Step 1: Validate every selected source strip has a 100-pixel width and height grid**

Run a read-only image-dimension check. Require the manifest's frame count to equal `(width / 100) * (height / 100)` for each action.

- [ ] **Step 2: Confirm exclusions are not part of the generation map**

Require the generated source list to contain no `Wizard`, `Effect`, or `(With magic effects)` path, and no unsuffixed `<Character>.png` atlas.

- [ ] **Step 3: Confirm the next data IDs are free**

Parse the three JSON tables and fail if UnitData IDs 12–29, StateScript IDs 11–28, or AnimationProfile IDs 6–23 already exist. Swordsman already owns UnitData ID 11 and AnimationProfile ID 5.

- [ ] **Step 4: Correct the existing Werewolf and Werebear preview PPtr ordering**

For each existing `.anim`, write the N sprite references in source-frame order inside `m_PPtrCurves.curve`, then write the N corresponding references in source-frame order inside `pptrCurveMapping`. Do not interleave or pair the two lists. Retain all existing GUIDs, clips, controllers, prefabs, data rows, and frame-library tracks.

### Task 2: Generate sprites, clips, preview controllers, and prefabs for the first six characters

**Files:**
- Modify: selected `.png.meta` files under `Archer`, `Armored Axeman`, `Armored Orc`, `Armored Skeleton`, `Bat`, and `Elite Orc` shadow folders.
- Create: `Assets/Res/Animation/<Character>/<Action>Left.anim`
- Create: `Assets/Res/Animation/<Character>/<Action>Right.anim`
- Create: `Assets/Res/Animation/<Character>/<Character>.controller`
- Create: `Assets/Res/Prefab/Unit/<Character>.prefab`

**Interfaces:**
- Consumes: Task 1 action/frame map; `MonsterStraw.prefab`; a known-good two-direction clip format from Werebear.
- Produces: 40 actions, 80 clips, 265 source frames, six complete prefabs.

- [ ] **Step 1: Slice and name each selected source strip**

Set each texture importer to Multiple sprites with 100x100 rectangles and names `<Character>_<Action>_<index>`. Retain each texture's existing GUID.

- [ ] **Step 2: Create two clips for every source action**

Use 12 fps. Idle and Walk loop; every other listed action does not loop. Right clips reference frames in source order with FlipX 0. Left clips reference the identical source order with FlipX 1.

- [ ] **Step 3: Create one controller per character**

Create left/right states for every generated action and set `<Default>Right` as default. For Bat, set `FlyingRight` as the default state.

- [ ] **Step 4: Copy and wire each prefab**

Copy `MonsterStraw.prefab`, set its root name to the character name, preserve `m_Enabled: 0` on Animator, assign the generated controller, and assign `Idle_0` (or `Flying_0` for Bat) to SpriteRenderer.

- [ ] **Step 5: Validate the batch locally**

For all six characters, assert sprite counts, 2× clip count, target texture GUID/frame IDs in both PPtr curves and mappings, controller state count, disabled Animator, and initial SpriteRenderer reference.

### Task 3: Generate sprites, clips, preview controllers, and prefabs for the next four characters

**Files:**
- Modify: selected `.png.meta` files under `Greatsword Skeleton`, `Knight`, `Knight Templar`, and `Lancer` shadow folders.
- Create: `Assets/Res/Animation/<Character>/...`
- Create: `Assets/Res/Prefab/Unit/<Character>.prefab`

**Interfaces:**
- Consumes: Task 1 action/frame map and the asset layout established in Task 2.
- Produces: 33 actions, 66 clips, 224 source frames, four complete prefabs.

- [ ] **Step 1: Slice all listed strips into 100x100 named sprites**

Use the exact source names in the manifest. Keep `Walk01` and `Walk02` as separate actions for Knight Templar and Lancer.

- [ ] **Step 2: Generate left/right clips and controllers**

Use the Task 2 clip convention. Set loops only on `Idle`, `Walk`, `Walk01`, and `Walk02`; all attacks, Block, Summon, Hurt, and Death clips are one-shot.

- [ ] **Step 3: Copy and wire the four prefabs**

Use Idle frame 0 in SpriteRenderer and assign each generated controller to its disabled Animator.

- [ ] **Step 4: Validate the batch locally**

Require the exact frame, clip, state, and reference totals stated by this task before continuing.

### Task 4: Generate sprites, clips, preview controllers, and prefabs for the final eight characters

**Files:**
- Modify: selected `.png.meta` files under `Necromancer`, `Orc`, `Orc rider`, `Priest`, `Skeleton`, `Skeleton Archer`, `Slime`, and `Soldier` shadow folders.
- Create: `Assets/Res/Animation/<Character>/...`
- Create: `Assets/Res/Prefab/Unit/<Character>.prefab`

**Interfaces:**
- Consumes: Task 1 action/frame map and Task 2 clip convention.
- Produces: 54 actions, 108 clips, 340 source frames, eight complete prefabs.

- [ ] **Step 1: Slice only character-body strips**

For Necromancer, include Attack01, Attack02, Summon, Idle, Walk, Hurt, and `DEATH`; normalize `DEATH` to action name `Death`. Exclude `Attack02_Effect`, `Attack02(With magic effects)`, `Sumon_Effect`, and `Summon(With magic effects)`. For Priest, include Attack, Heal, Idle, Walk, Hurt, and Death; exclude `Attack_effect`, `Attack(With magic effects)`, `Heal_effect`, and `Heal(With magic effects)`.

- [ ] **Step 2: Generate clips and controllers**

Use source-order frames and the two-direction FlipX convention. Only Idle and Walk loop. Include every listed non-spell body action as a controller state pair.

- [ ] **Step 3: Copy and wire the eight prefabs**

Assign Idle frame 0 and each controller to the prefab's existing disabled Animator.

- [ ] **Step 4: Validate the batch locally**

Require exact per-action frame counts and 108 target clip files before continuing.

### Task 5: Integrate all 18 characters with runtime data

**Files:**
- Modify: `Assets/Res/Data/UnitDataTable.json`
- Modify: `Assets/Res/Data/UnitAnimationProfileDataTable.json`
- Modify: `Assets/Res/Data/StateScriptDataTable.json`
- Modify: `Assets/Res/Data/UnitAnimationFrameLibrary.asset`

**Interfaces:**
- Consumes: all clips and prefabs from Tasks 2–4.
- Produces: 18 UnitData entries, 18 animation profiles, 18 independent idle state graphs, and 254 frame-library tracks.

- [ ] **Step 1: Append UnitData rows 12–29**

Clone the Werewolf UnitData row (ID 9), set `Id` to 12–29 in manifest order, set `Name` and `Description` to the character name, and set `PrefabPath` to `Assets/Res/Prefab/Unit/<Character>.prefab`.

- [ ] **Step 2: Append AnimationProfile rows 6–23**

For every source action, create an entry with DirectionMode 1 and the matching `Assets/Res/Animation/<Character>/<Action>Left.anim` and `...Right.anim` paths. Preserve `Walk01` and `Walk02` as separate names. Bat's profile default action is `Flying`.

- [ ] **Step 3: Append StateScript rows 11–28**

Clone the Werewolf ID 9 idle graph for each row, replace every 32-hex graph/node GUID with a distinct new GUID, and retain the graph as Idle. For Bat only, replace the StateScript's animation-name literal with `Flying` so it does not request an absent Idle action.

- [ ] **Step 4: Append one frame-library track per generated clip**

Write 254 tracks with exact `SourceClip` GUIDs, sprite file IDs, sprite times at 12 fps, length, loop flag, and FlipX values. Each track must reference only its own character's source texture.

- [ ] **Step 5: Write the three JSON files with UTF-8 BOM**

Verify the first bytes of each JSON file are `EF BB BF` after writing.

### Task 6: Run final asset and data integrity verification

**Files:**
- Verify: all files listed in Tasks 2–5.

**Interfaces:**
- Consumes: complete batch output.
- Produces: evidence that the full set imports and references correctly.

- [ ] **Step 1: Parse JSON and validate all allocated IDs**

Require exactly one UnitData row for each ID 12–29, exactly one StateScript row for each ID 11–28, exactly one AnimationProfile row for each ID 6–23, no collision between cloned StateScript GUIDs and the source Werewolf graph, and UTF-8 BOM on all three files.

- [ ] **Step 2: Validate every generated asset reference**

Require 18 prefabs, 18 controllers, 254 clips, 254 frame-library tracks, and 18 controller default states. Confirm every SpriteRenderer uses the character default frame and every Animator remains disabled while referencing its own controller.

- [ ] **Step 3: Validate exclusions and workspace cleanliness**

Require no new Wizard assets, no generated assets for excluded spell-only source strips, no helper `.ps1`/`.cs` files, and no whitespace errors from `git diff --check`.

- [ ] **Step 4: Let Unity process the assets and re-run reference checks**

After the AssetDatabase import settles, re-read the texture metas, clips, profiles, and library to confirm all selected sprite counts and GUID/fileID references remain unchanged.

## Self-Review

- Spec coverage: Tasks 2–4 cover all 18 selected characters and every manifest action. Task 5 covers prefab runtime integration. Task 6 checks slice, animation, controller, prefab, JSON encoding, exclusion, and import stability requirements.
- Placeholder scan: no implementation steps rely on unspecified characters, IDs, action names, source folders, or validation targets.
- Type consistency: all profile paths map to the exact clip filenames generated by Tasks 2–4; manifest-order UnitData IDs 12–29 pair with StateScript IDs 11–28 and profile IDs 6–23.
