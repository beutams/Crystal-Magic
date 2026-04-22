---
name: crystal-magic-game-component
description: Create or update Crystal Magic Unity GameComponent, UI, or ECS code. Use when adding a new component derived from GameComponent<T>, wiring it into Assets/Scripts/Core/GameEntry.cs, choosing initialization Priority order, managing runtime GameObjects through ResourceComponent and PoolComponent, routing mouse/keyboard input through InputComponent events, creating UI that must follow the MVC model-refresh pattern, or generating ECS Authoring/Baker/Component/System/Job files.
---

# Crystal Magic GameComponent

Use this skill when adding or modifying a lifecycle-managed component in the Crystal Magic Unity project.

## Core Rules

- Create component scripts under `Assets/Scripts/Core/<Domain>/` unless the existing project structure clearly points elsewhere.
- Put runtime components in namespace `CrystalMagic.Core`.
- Derive MonoBehaviour-managed game systems from `GameComponent<TComponent>`.
- Use `public override int Priority => <number>;` when startup order matters.
- Let `GameEntry` initialize and clean up components through `Initialize()` and `Cleanup()`.
- Do not generate GameObject creation, `AddComponent`, editor setup code, or bootstrap code for attaching the component. The user manually attaches GameComponents to GameObjects.
- Do not create a separate service locator or registration system; use the existing `GameEntry` pattern.
- Route runtime GameObject loading, spawning, borrowing, and releasing through `ResourceComponent` and `PoolComponent`.
- Route mouse and keyboard input through `InputComponent` events; external modules subscribe to those events instead of polling input directly.
- Build UI with the project's MVC pattern: View subscribes to Model change events, and Model property changes trigger View refresh/render functions.
- For new ECS code, prefer struct-based `IComponentData`, `ISystem`, `IJobEntity`, and `[BurstCompile]`; use class-based ECS only when required by managed data or APIs.

## Component Template

Prefer this shape for a new component:

```csharp
using UnityEngine;

namespace CrystalMagic.Core
{
    public class ExampleComponent : GameComponent<ExampleComponent>
    {
        public override int Priority => 100;

        public override void Initialize()
        {
            base.Initialize();
            // Initialize dependencies and runtime state here.
        }

        public override void Cleanup()
        {
            // Release subscriptions/resources owned by this component.
            base.Cleanup();
        }
    }
}
```

Only add `Awake`, `Start`, or `Update` when the component genuinely needs Unity callbacks. Keep cross-component initialization in `Initialize()` so `GameEntry` controls ordering.

## GameEntry Wiring

When adding a new `GameComponent<T>`, update `Assets/Scripts/Core/GameEntry.cs` in three places:

1. Add a public property near the existing component properties:

```csharp
public ExampleComponent ExampleComponent { get; private set; }
```

2. Add registration in `InitializeAllComponents()` with the existing manual registrations:

```csharp
RegisterComponent(ExampleComponent.Instance);
```

3. Add an assignment branch in `RegisterComponent(IGameComponent component)`:

```csharp
else if (component is ExampleComponent exampleComponent)
    ExampleComponent = exampleComponent;
```

Preserve the existing pattern: `RegisterComponent` accepts `null`, stores every non-null component in `_components`, then assigns the strongly typed property.

## Runtime GameObject Flow

When code needs to get or generate a gameplay, UI, VFX, projectile, audio helper, or other runtime `GameObject`, use the project's resource and pooling layer.

- Use `ResourceComponent.Instance.Load<T>(assetPath)` for loading non-GameObject assets or prefab references.
- Use `PoolComponent.Instance.Get(assetPath)` when a runtime instance should be obtained by asset path.
- Use `PoolComponent.Instance.Get(prefab)` only when a prefab reference is already available and the instance must still be pool-managed.
- Use `PoolComponent.Instance.Release(gameObject)` when returning an object that came from the pool.
- Do not call `Object.Instantiate`, `Object.Destroy`, or `new GameObject` in gameplay/UI feature code to create managed runtime objects.
- Keep direct `Instantiate` and pool container creation inside the pooling infrastructure itself, such as `PoolComponent` or `GameObjectPool`, when those files are intentionally being modified.

Prefer this shape:

```csharp
GameObject instance = PoolComponent.Instance.Get(assetPath);
if (instance == null)
{
    Debug.LogError($"[ExampleComponent] Failed to get object: {assetPath}");
    return;
}

// Use instance...

PoolComponent.Instance.Release(instance);
```

If an existing feature currently clones prefabs directly, migrate the touched path to `ResourceComponent` + `PoolComponent` while preserving behavior.

## Input Event Flow

Mouse and keyboard operations belong in `InputComponent`. Other modules should receive intent by subscribing to events.

- Add new input events to `InputComponent` when a new mouse or keyboard action is needed.
- Wire the event from `InputControls`, `Mouse.current`, or `Keyboard.current` inside `InputComponent`.
- Subscribe from external modules with `InputComponent.Instance.OnX += HandleX`.
- Unsubscribe in the matching cleanup path with `InputComponent.Instance.OnX -= HandleX`.
- Do not call `Input.GetKey`, `Keyboard.current`, `Mouse.current`, or `InputAction` polling from gameplay, UI, ECS systems, or other feature modules unless the file is `InputComponent` itself.
- For ECS systems, follow the existing pattern in `PlayerInputSystem` and `NPCInteractInputSystem`: subscribe once when `InputComponent.Instance` becomes available, store the latest input in system-owned state, and unsubscribe in `OnDestroy`.
- Keep game-lock decisions in consumers when the action meaning is domain-specific, using existing gates such as `GameGateComponent.Instance.IsPlayerInputLocked` or `IsUIInputLocked`.

Prefer this external subscription shape:

```csharp
if (!_subscribed && InputComponent.Instance != null)
{
    InputComponent.Instance.OnInteract += HandleInteract;
    _subscribed = true;
}
```

And the matching cleanup:

```csharp
if (_subscribed && InputComponent.Instance != null)
{
    InputComponent.Instance.OnInteract -= HandleInteract;
}
```

## UI MVC Flow

All UI panels must follow the existing MVC framework:

- View classes derive from `UIBase` or `UIBase<TData>`.
- Model classes derive from `UIModelBase`.
- Controller classes derive from `UIControllerBase<TView, TModel>`.
- Name related files consistently as `<PanelName>.cs`, `<PanelName>Model.cs`, `<PanelName>Controller.cs`, and `<PanelName>Data.cs` when generated data bindings exist.
- Let `UIComponent` create and bind MVC contexts; do not hand-roll controller/model construction outside the established UI flow.

Keep responsibilities separated:

- View owns Unity references, button listeners, visual rendering, and user-intent events such as `BackClicked`.
- Model owns UI state and exposes read-only state/properties to the View.
- Controller subscribes to View intent events, talks to game systems, updates the Model, and handles opening/closing child UI.
- View must not pull gameplay/save/runtime data directly from components when a Controller can push that data into the Model.
- Controller should not directly mutate Unity visual elements when the View can expose a refresh/render function.

Model changes must notify the View:

- Add a Model change event for every state group the View needs to refresh, such as `DataChanged`, `SaveRecordsChanged`, or `SelectionChanged`.
- When a Model property or backing state changes, publish/raise the change event from the Model immediately after the value is committed.
- The View binds to the Model through a `BindModel(ModelType model)` method or equivalent.
- The View subscribes to Model events while open, unsubscribes on close or model replacement, and calls a View refresh/render function from the event handler.
- Event handlers must ignore events from other Model instances when using `EventComponent` by checking the event payload model against the currently bound model.

Prefer this Model shape when following the existing `EventComponent` pattern:

```csharp
public sealed class ExampleUIModel : UIModelBase
{
    public const string DataChangedEventName = "ExampleUIModel.DataChanged";

    public string Title { get; private set; }

    public void SetTitle(string title)
    {
        if (Title == title)
            return;

        Title = title;
        EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
    }
}
```

Prefer this View shape:

```csharp
private ExampleUIModel _model;
private bool _isOpened;
private bool _isModelEventSubscribed;

public void BindModel(ExampleUIModel model)
{
    if (_model == model)
        return;

    if (_model != null && _isOpened)
        UnsubscribeModelEvents();

    _model = model;

    if (_model != null && _isOpened)
    {
        SubscribeModelEvents();
        RefreshView();
    }
}

public override void OnOpen()
{
    _isOpened = true;
    SubscribeModelEvents();
    RefreshView();
}

public override void OnClose()
{
    UnsubscribeModelEvents();
    _isOpened = false;
}

private void OnModelChanged(CommonGameEvent gameEvent)
{
    ExampleUIModel eventModel = gameEvent.GetData<ExampleUIModel>();
    if (eventModel != _model)
        return;

    RefreshView();
}
```

Prefer this Controller shape:

```csharp
protected override void OnOpen()
{
    View.BindModel(Model);
    Bindings.Bind(() => View.BackClicked += OnBackClicked, () => View.BackClicked -= OnBackClicked);
    Model.SetTitle("Title");
}
```

## ECS File Flow

When generating new ECS-related files, optimize for data-oriented, Burst-friendly code first.

Prefer this order:

1. Use `struct` ECS components and systems where Unity Entities supports it.
2. Use `IJobEntity` or another Burst-compatible job for per-entity work.
3. Mark systems/jobs with `[BurstCompile]` when their code path is unmanaged and Burst-compatible.
4. Use `class`, `SystemBase`, or managed `IComponentData` only when the feature requires managed references, dictionaries, strings, UnityEngine objects, managed callbacks, or APIs that cannot run in Burst/jobs.

Authoring and component files:

- Put one Authoring, its nested Baker, and its corresponding Component in the same `.cs` file.
- Name the file after the Authoring, such as `UnitMoveAuthoring.cs`.
- Keep exactly one corresponding Component per Authoring file.
- Do not put multiple unrelated `IComponentData` types in one Authoring file.
- Do not make one Authoring/Baker add several independent gameplay Components. Split them into separate Authoring files instead.
- Prefer `public struct XComponent : IComponentData` for the component.
- Use `public class XComponent : IComponentData` with `AddComponentObject` only when unmanaged `struct IComponentData` cannot represent the required state.

Prefer this Authoring shape:

```csharp
using Unity.Entities;
using UnityEngine;

public class ExampleAuthoring : MonoBehaviour
{
    class ExampleBaker : Baker<ExampleAuthoring>
    {
        public override void Bake(ExampleAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new ExampleComponent
            {
                Value = authoring.Value,
            });
        }
    }

    public float Value = 1f;
}

public struct ExampleComponent : IComponentData
{
    public float Value;
}
```

System and job files:

- Prefer `partial struct XSystem : ISystem` over `SystemBase`.
- Prefer a paired `partial struct XJob : IJobEntity` for entity iteration.
- Put the System and its corresponding Job in the same `.cs` file when they are tightly paired.
- Keep one same-named System pair per `.cs` file: `XSystem` plus only its corresponding `XJob` or small helper job.
- Do not group multiple unrelated Systems or Jobs into one file.
- Use `[BurstCompile]` on the System and Job when possible.
- Schedule parallel jobs with `ScheduleParallel()` when the job has no unsafe shared writes.
- Use `state.RequireForUpdate<T>()` or equivalent query requirements when a system should only run with required data.

Prefer this System/Job shape:

```csharp
using Unity.Burst;
using Unity.Entities;

[BurstCompile]
partial struct ExampleSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ExampleComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new ExampleJob().ScheduleParallel();
    }
}

[BurstCompile]
public partial struct ExampleJob : IJobEntity
{
    public void Execute(ref ExampleComponent component)
    {
        component.Value += 1f;
    }
}
```

Use a class-based ECS type only with a clear reason in the code shape:

- `SystemBase`: when the system must use managed ECS APIs, managed components, or managed Unity APIs that do not fit `ISystem`.
- `class IComponentData`: when the component must hold managed state, such as `Dictionary`, `string` state that must be mutated as managed data, Unity object references, or state machine object instances.
- Managed code paths should be isolated from Burst jobs and kept as small as practical.

## Initialization Order

`GameEntry` sorts registered components by `Priority` ascending before calling `Initialize()`. Lower numbers initialize earlier; cleanup runs in reverse registration list order from `_components.Count - 1` to `0`.

Use existing priorities as anchors:

- `GameGateComponent`: `4`
- `InputComponent`: `5`
- `ResourceComponent`: `5`
- `ConfigComponent`: `8`
- `EventComponent`: `10`
- `DataComponent`: `11`
- `PoolComponent`: `12`
- `CameraComponent`: `13`
- `UIComponent`: `15`
- `SaveDataComponent`: `18`
- `SceneComponent`: `20`
- `TransitionComponent`: `25`
- `AudioComponent`: `28`
- `GameFlowComponent`: `30`

Choose a priority by dependency:

- If the new component reads another component during `Initialize()`, give the dependency a lower priority.
- If another component reads the new component during `Initialize()`, give the new component a lower priority than that consumer.
- If the component only exposes APIs used later, default to `100` or choose a sparse value after its dependencies.
- If two components share the same priority, do not rely on list insertion order unless the existing code already does so intentionally.

## Dependency Style

- Access other game components through `OtherComponent.Instance` or the `GameEntry` property pattern already used by the project.
- Guard optional dependencies with null checks.
- Avoid doing scene transitions, asset loads, or UI opens in `Awake`; wait for `Initialize()` unless Unity callback timing is explicitly required.
- Unsubscribe events and clear owned collections in `Cleanup()`, then call `base.Cleanup()` last.

## Generated Factory Pattern

When runtime code creates concrete classes by key, use the shared `GeneratedFactory + AutoGeneratedRegistry` pattern instead of hand-written dictionaries, switches, or per-feature registration code.

- Put reusable factory behavior in `Assets/Scripts/Core/Factory/GeneratedFactory.cs`.
- Keep generated registrations in `Assets/Scripts/Core/Factory/AutoGeneratedRegistry.cs`.
- Generate the registry through `Assets/Scripts/Core/Factory/Editor/AutoGeneratedRegistryGenerator.cs`.
- Let business types declare mapping rules with `[FactoryKey("Key", order, "Display Name")]`.
- Use `[FactoryInputMember("value")]` when a generated factory lambda needs to copy an input value into a public writable float member.
- Domain factories should be thin wrappers around `GeneratedFactory<TKey, TValue>` or `GeneratedFactory<TKey, TInput, TValue>` and expose only domain names such as `CreateState`, `CreateNode`, or `BuildComparator`.
- Legacy domain registry classes may remain as compatibility facades, but they should delegate to `AutoGeneratedRegistry`; do not generate concrete registration calls into those business-facing classes.

Prefer this shape for a new runtime key-to-type family:

```csharp
[FactoryKey("Example")]
public sealed class ExampleRuntimeType : ExampleBase
{
}

public sealed class ExampleFactory : GeneratedFactory<string, ExampleBase>
{
    public ExampleBase CreateExample(string key) => Create(key);
}
```

Then extend `AutoGeneratedRegistryGenerator` with the family scan and generated `RegisterExampleTypes(ExampleFactory factory)` method. Runtime initialization calls only that generated registry method.

## Final Check

Before finishing a GameComponent change, verify:

- The new class derives from `GameComponent<NewComponent>`.
- `GameEntry` has a property, a `RegisterComponent(NewComponent.Instance)` call, and a type assignment branch.
- The selected `Priority` matches initialization dependencies.
- The code does not create GameObjects or call `AddComponent` for the GameComponent itself.
- Runtime GameObject acquisition/creation/release goes through `ResourceComponent` and `PoolComponent`.
- Mouse and keyboard behavior goes through `InputComponent` events and external subscriptions.
- UI code follows MVC: View subscribes to Model change events, Model property changes publish/raise events, and View refresh/render functions update visuals.
- ECS code prefers struct + Job + Burst; class-based ECS is used only when managed data or APIs require it.
- New ECS Authoring files contain exactly one Authoring, its Baker, and one corresponding Component.
- New ECS System files contain exactly one System and its corresponding same-feature Job when a Job is needed.
- Runtime key-to-type creation uses `GeneratedFactory + AutoGeneratedRegistry`, with mappings declared on concrete business types.
- The implementation compiles with the project namespace and existing Unity patterns.
