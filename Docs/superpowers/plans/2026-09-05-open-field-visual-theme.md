# 开放场视觉主题 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将正式 Open Field 地下城改为由主题配置驱动的 RuleTile 地形、地皮装饰与逐格障碍物，并在运行时烘焙为 Back/Top 两层视觉。

**Architecture:** 纯 C# 的视觉布局构建器先从高度地图和主题路径配置生成 RuleTile 占格、Style 分区及障碍物实例；场景数据构建器将它们与原有 ECS 内容生成整合。运行时创建临时 Grid/Tilemap 让 Unity RuleTile 选图，再把已解析的 Sprite 烘焙为两个网格层；独立障碍物继续作为 ECS Environment 实体生成。

**Tech Stack:** Unity 6000.1.17f1、C# 9、Unity Tilemap + 2D Tilemap Extras RuleTile、Entities 1.3.15、Entities Graphics 1.4.18、Unity Test Framework 1.5.1、Newtonsoft JSON。

**Spec:** `Docs/superpowers/specs/2026-09-05-open-field-visual-theme-design.md`

## 全局约束

- Void 永远为 `-1`，Ground 永远为 `0`，Obstacle 必须是 `+1...+N` 的整数台阶。
- Void 的三种图只能写入一张 Void Tilemap；Obstacle 的三种图只能写入一张 Obstacle Tilemap；基础地皮和装饰分别使用自己的 Tilemap。
- 15 格、16 格及单格美术均只通过 Unity RuleTile 解析；不得保留或新增手写的 15/16 格邻居选图器。
- 主题 JSON 只保存可由 `ResourceComponent` 加载的资产路径；编辑器对象选择器不得把 Unity 对象序列化进 JSON。
- Ground Style 必须覆盖每个零高度格；装饰不得跨 Style，且离 Style 边缘至少一格。
- 障碍物碰撞掩码格必须在 Ground 上，八邻格也必须为 Ground；禁止事后全局洪泛修路。
- 保留玩家、出口、宝箱和怪物的现有 ECS 生成链路；障碍 Sprite 用已有 `Environment` ECS 预制体渲染，碰撞格用已有 `Collider` ECS 预制体。
- 删除旧主题行、旧 3×3 地形数据和 MapTest 高度图缓存，但绝不删除源 Sprite 或 RuleTile 美术资产。
- 每个任务结束运行对应 Unity EditMode 测试和 `dotnet build Assembly-CSharp.csproj -nologo --no-restore -v:q`；只提交该任务涉及的文件。

---

## 文件结构

| 路径 | 责任 |
| --- | --- |
| `Assets/Scripts/Game/Data/DungeonDefinitionData.cs` | 新主题视觉路径数据、Ground Style、装饰与障碍物定义和验证。 |
| `Assets/Scripts/Game/OpenField/OpenFieldDungeonTerrainConfig.cs` | Obstacle 最小/最大高度配置。 |
| `Assets/Scripts/Game/OpenField/OpenFieldDungeonLayout.cs` | 保留每格整数高度。 |
| `Assets/Scripts/Game/OpenField/OpenFieldDungeonTerrainGenerator.cs` | 将连续噪声分类为 Void/Ground/Obstacle 的整数高度。 |
| `Assets/Scripts/Game/OpenField/OpenFieldDungeonVisualLayoutBuilder.cs` | 纯逻辑：Style 扩张、装饰簇、障碍物放置与 RuleTile 占格。 |
| `Assets/Scripts/AssemblyInfo.cs` | 为 EditMode 测试开放内部场景数据构建器。 |
| `Assets/Scripts/Core/Runtime/RuntimeDataComponent.cs` | 运行时 RuleTile 放置、地形视觉和障碍物 spawn 数据。 |
| `Assets/Scripts/Core/Runtime/OpenFieldDungeonSceneDataBuilder.cs` | 把视觉布局、碰撞格和既有内容合成为场景数据。 |
| `Assets/Scripts/Core/Runtime/DungeonRuleTileVisualBuilder.cs` | 临时 Tilemap 解析和 Back/Top 纹理/网格烘焙。 |
| `Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs` | 调用地形烘焙并生成障碍 Sprite 与逐格碰撞实体。 |
| `Assets/Scripts/Core/Runtime/DungeonSceneVisualUtility.cs` | 根据 Sprite 路径构建障碍的 Entities Graphics quad、材质和排序深度。 |
| `Assets/Scripts/Core/Runtime/DungeonSceneRuntimeRoot.cs` | 追踪并销毁烘焙纹理、网格和材质。 |
| `Assets/Scripts/Game/Data/Editor/DungeonEditorWindow.OpenField.cs` | 主题视觉配置的对象选择器、列表和碰撞掩码 UI。 |
| `Assets/Scripts/Game/Data/DungeonTileGridData.cs` | 删除：旧 3×3 选图数据。 |
| `Assets/Scripts/Game/Data/Editor/TileGridPreviewWindow.cs` | 删除：旧 3×3 图格编辑窗口。 |
| `Assets/Scripts/Core/Runtime/DungeonTileVisualBuilder.cs` | 删除：旧的固定 Sprite quad 地形渲染器。 |
| `Assets/Scripts/Game/MapDemo/OpenFieldMapTestDemo.cs` | 去掉旧高度图文件缓存，只保留内存预览。 |
| `Assets/Scripts/Game/MapDemo/Editor/OpenFieldMapTestDemoEditor.cs` | 去掉“保存并在下次 Play 读取缓存”的 UI 文案和流程。 |
| `Assets/Scripts/Game/MapDemo/OpenFieldMapPlayDemo.cs` | 删除：旧的独立色块/树木试玩场。 |
| `Assets/Res/Data/DungeonThemeDataTable.json` | 改为空 `Rows` 表。 |
| `Assets/Resources/MapTest/LatestHeightMap.png` | 删除：过时的 MapTest 缓存及其 `.meta`。 |
| `Assets/Tests/Editor/OpenFieldVisualThemeTests.cs` | EditMode 覆盖高度、数据验证、布局、间隔和烘焙组合。 |

## Task 1: 高度语义与 EditMode 测试基础

**Files:**

- Create: `Assets/Tests/Editor/OpenFieldVisualThemeTests.cs`
- Create: `Assets/Scripts/AssemblyInfo.cs`
- Modify: `Assets/Scripts/Game/OpenField/OpenFieldDungeonTerrainConfig.cs`
- Modify: `Assets/Scripts/Game/OpenField/OpenFieldDungeonLayout.cs`
- Modify: `Assets/Scripts/Game/OpenField/OpenFieldDungeonTerrainGenerator.cs`

**Interfaces:**

- Produces `int OpenFieldDungeonLayout.GetHeightSteps(int x, int y)`。
- Produces `internal void OpenFieldDungeonLayout.SetTerrain(int x, int y, float terrainValue, OpenFieldTerrainCell terrainCell, int heightSteps)`。
- Produces `OpenFieldDungeonTerrainConfig.MinimumObstacleHeight` 和 `MaximumObstacleHeight`。

- [ ] **Step 1: 写出失败的高度规则测试**

  在 `OpenFieldVisualThemeTests` 写入：

  ```csharp
  [Test]
  public void Generate_AssignsFixedHeightsForEveryTerrainCategory()
  {
      OpenFieldDungeonTerrainConfig config = new()
      {
          Width = 48,
          Height = 48,
          LowToGroundThreshold = 0.40f,
          GroundToObstacleThreshold = 0.55f,
          MinimumObstacleHeight = 1,
          MaximumObstacleHeight = 4,
      };

      OpenFieldDungeonLayout layout = OpenFieldDungeonTerrainGenerator.Generate(712367, config);
      for (int y = 0; y < layout.Height; y++)
      for (int x = 0; x < layout.Width; x++)
      {
          int height = layout.GetHeightSteps(x, y);
          Assert.That(height, Is.EqualTo(-1).Or.GreaterThanOrEqualTo(0));
          switch (layout.GetTerrainCell(x, y))
          {
              case OpenFieldTerrainCell.Void: Assert.That(height, Is.EqualTo(-1)); break;
              case OpenFieldTerrainCell.Ground: Assert.That(height, Is.EqualTo(0)); break;
              case OpenFieldTerrainCell.Obstacle: Assert.That(height, Is.InRange(1, 4)); break;
          }
      }
  }
  ```

- [ ] **Step 2: 运行测试确认失败**

  Run:

  ```powershell
  & 'E:\Unity\Editor\6000.1.17f1\Editor\Unity.exe' -batchmode -quit -projectPath 'E:\Unity\Project\Crystal Magic' -runTests -testPlatform EditMode -testResults 'Temp\OpenFieldVisualThemeTests.xml'
  ```

  Expected: `GetHeightSteps` 或高度配置字段不存在，测试编译失败。

- [ ] **Step 3: 最小化实现整数高度**

  在 `OpenFieldDungeonTerrainConfig` 加入并验证：

  ```csharp
  public int MinimumObstacleHeight = 1;
  public int MaximumObstacleHeight = 4;
  ```

  在 `OpenFieldDungeonLayout` 加入 `int[] _heightSteps` 和 `GetHeightSteps`；扩展 `SetTerrain` 同时写入类别与高度。地形生成器把低阈值以下写成 `-1`，中间写成 `0`，高阈值以上用相对阈值位置映射到 `[MinimumObstacleHeight, MaximumObstacleHeight]` 的整数区间。`CloneValidated` 必须复制这两个字段。

  同时创建 `Assets/Scripts/AssemblyInfo.cs`，使 EditMode 测试可以直接验证内部场景构建器：

  ```csharp
  using System.Runtime.CompilerServices;
  [assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
  ```

- [ ] **Step 4: 运行测试与编译**

  重新运行 Step 2 命令，再运行：

  ```powershell
  dotnet build Assembly-CSharp.csproj -nologo --no-restore -v:q
  ```

  Expected: EditMode 通过，编译零错误。

- [ ] **Step 5: 提交**

  ```powershell
  git add Assets/Scripts/AssemblyInfo.cs Assets/Scripts/Game/OpenField/OpenFieldDungeonTerrainConfig.cs Assets/Scripts/Game/OpenField/OpenFieldDungeonLayout.cs Assets/Scripts/Game/OpenField/OpenFieldDungeonTerrainGenerator.cs Assets/Tests/Editor/OpenFieldVisualThemeTests.cs
  git commit -m "feat: add open field terrain height steps"
  ```

## Task 2: 主题视觉数据与编辑器迁移

**Files:**

- Modify: `Assets/Scripts/Game/Data/DungeonDefinitionData.cs`
- Modify: `Assets/Scripts/Game/Data/Editor/DungeonEditorWindow.OpenField.cs`
- Delete: `Assets/Scripts/Game/Data/DungeonTileGridData.cs` 和 `.meta`
- Delete: `Assets/Scripts/Game/Data/Editor/TileGridPreviewWindow.cs` 和 `.meta`
- Modify: `Assets/Res/Data/DungeonThemeDataTable.json`
- Modify: `Assets/Tests/Editor/OpenFieldVisualThemeTests.cs`

**Interfaces:**

- Produces `OpenFieldDungeonVisualData.VoidVisual`、`ObstacleVisual` 和 `List<OpenFieldGroundStyleData> GroundStyles`。
- Produces `OpenFieldGroundStyleData.BaseRuleTile.AssetPath`、`List<OpenFieldDecorationData> Decorations`、`List<OpenFieldObstacleData> Obstacles`。
- Produces `OpenFieldDungeonVisualData.GroundCellsPerStyleSeed`，用来把每个连通 Ground 区域的面积转换为 Style 种子数。
- Produces `OpenFieldSpriteReferenceData.AssetPath`、`SpriteName`、`SpriteUv`、`HasSpriteUv`。

- [ ] **Step 1: 写出失败的数据验证与空表测试**

  在同一测试文件加入：

  ```csharp
  [Test]
  public void VisualEnsureValid_InitializesAllListsAndClampsObstacleMasks()
  {
      OpenFieldDungeonVisualData visual = new();
      visual.EnsureValid();

      Assert.That(visual.GroundStyles, Is.Not.Null);
      Assert.That(visual.VoidVisual, Is.Not.Null);
      Assert.That(visual.ObstacleVisual, Is.Not.Null);
  }
  ```

  另加入 JSON 文本断言：读取 `Assets/Res/Data/DungeonThemeDataTable.json`，反序列化包装器后断言 `Rows.Count == 0`。

- [ ] **Step 2: 运行测试确认失败**

  Run: Task 1 的 Unity EditMode 命令。

  Expected: 新类型和字段不存在，或主题表仍有 Cave/Hell 行。

- [ ] **Step 3: 用路径模型替换旧 3×3 数据**

  在 `DungeonDefinitionData.cs` 删除 `DungeonTileGridData` 字段，加入以下可序列化数据边界：

  ```csharp
  public sealed class OpenFieldRuleTileReferenceData { public string AssetPath; }
  public sealed class OpenFieldSpriteReferenceData
  {
      public string AssetPath;
      public string SpriteName;
      public Vector4 SpriteUv;
      public bool HasSpriteUv;
  }
  public sealed class OpenFieldDecorationData
  {
      public string Name;
      public OpenFieldRuleTileReferenceData RuleTile = new();
      public float Radius = 8f;
      public int MaximumSpread = 8;
  }
  ```

  `OpenFieldDungeonVisualData.GroundCellsPerStyleSeed` 默认 `480` 且最小为 `1`。`OpenFieldObstacleData` 必须包含 Sprite 引用、宽高、`List<bool> CollisionMask`、Weight、MinimumSpacing、MaximumCount、AllowRotation、AllowFlipX 和 `Vector2 VisualSortAnchor`。`EnsureValid` 要把每个碰撞掩码强制为 `FootprintWidth * FootprintHeight` 项，保留已有值并补 `false`。

- [ ] **Step 4: 重建编辑器面板**

  在 `DungeonEditorWindow.OpenField` 删除三个 `DrawTerrainGridButton` 调用，改为：

  ```csharp
  private static string DrawRuleTilePath(string label, string path)
  {
      RuleTile current = string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<RuleTile>(path);
      RuleTile next = (RuleTile)EditorGUILayout.ObjectField(label, current, typeof(RuleTile), false);
      return next == null ? string.Empty : AssetDatabase.GetAssetPath(next);
  }
  ```

  六个地形 RuleTile 使用该 helper。Style、装饰、障碍物都显示增删按钮与折叠项；Sprite 使用 `EditorGUILayout.ObjectField`，保存 PNG 资产路径、子 Sprite 名称和 UV；障碍物显示宽×高布尔格阵列。修改任一项时调用 `EnsureValid()` 并设置 `_isDirty = true`。

- [ ] **Step 5: 删除旧数据并写入空主题表**

  删除 `DungeonTileGridData` 与 `TileGridPreviewWindow` 的 `.cs/.meta`，确认 `rg "DungeonTileGridData|TileGridPreviewWindow" Assets` 不再返回代码引用。将 JSON 重写为：

  ```json
  {
    "Rows": []
  }
  ```

  原始 Sprite、RuleTile 和环境预制体不得改动。

- [ ] **Step 6: 运行测试与编译**

  运行 Task 1 的 Unity EditMode 命令和 `dotnet build Assembly-CSharp.csproj -nologo --no-restore -v:q`。

  Expected: 数据验证测试通过；Dungeon Editor 打开时能创建空白主题、拖入 RuleTile/Sprite、保存后 JSON 只出现路径字段。

- [ ] **Step 7: 提交**

  ```powershell
  git add Assets/Scripts/Game/Data/DungeonDefinitionData.cs Assets/Scripts/Game/Data/Editor/DungeonEditorWindow.OpenField.cs Assets/Res/Data/DungeonThemeDataTable.json Assets/Tests/Editor/OpenFieldVisualThemeTests.cs
  git rm Assets/Scripts/Game/Data/DungeonTileGridData.cs Assets/Scripts/Game/Data/DungeonTileGridData.cs.meta Assets/Scripts/Game/Data/Editor/TileGridPreviewWindow.cs Assets/Scripts/Game/Data/Editor/TileGridPreviewWindow.cs.meta
  git commit -m "feat: configure open field visual themes"
  ```

## Task 3: 纯逻辑视觉布局构建器

**Files:**

- Create: `Assets/Scripts/Game/OpenField/OpenFieldDungeonVisualLayoutBuilder.cs`
- Modify: `Assets/Tests/Editor/OpenFieldVisualThemeTests.cs`

**Interfaces:**

- Produces `OpenFieldDungeonVisualLayout OpenFieldDungeonVisualLayoutBuilder.Build(OpenFieldDungeonLayout layout, OpenFieldDungeonVisualData visual, IReadOnlyCollection<Vector2Int> protectedCells)`。
- Produces `int OpenFieldDungeonVisualLayout.GetGroundStyleIndex(int x, int y)`、`bool IsStyleInterior(Vector2Int cell, int styleIndex)`、`bool IsValidObstacleCollisionCell(Vector2Int cell)`、`IReadOnlyList<OpenFieldRuleTilePlacement> RuleTilePlacements` 和 `IReadOnlyList<OpenFieldObstaclePlacement> Obstacles`。
- Consumes Task 1 height map and Task 2 validated data objects only; it must not load Unity assets or create GameObjects.

- [ ] **Step 1: 写出失败的 Style、装饰与障碍间隔测试**

  加入一个 48×48 生成布局的测试，两个 Style 使用不同的假 RuleTile 路径，装饰 `Radius = 5, MaximumSpread = 6`，障碍物是 `2×2` 且掩码为 `{ true, false, true, true }`。断言：

  ```csharp
  OpenFieldDungeonVisualLayout result = OpenFieldDungeonVisualLayoutBuilder.Build(layout, visual, protectedCells);
  Assert.That(result.GetGroundStyleIndex(x, y), Is.GreaterThanOrEqualTo(0)); // 每个 Ground 格
  Assert.That(result.IsStyleInterior(placement.Cell, placement.GroundStyleIndex), Is.True); // 每个装饰格
  Assert.That(result.IsValidObstacleCollisionCell(cell), Is.True); // 每个障碍碰撞格
  ```

  `protectedCells` 包含出生点、出口和每个内容格，断言它们不会落入任一障碍碰撞格。

- [ ] **Step 2: 运行测试确认失败**

  Run: Task 1 的 Unity EditMode 命令。

  Expected: `OpenFieldDungeonVisualLayoutBuilder` 与其结果类型不存在。

- [ ] **Step 3: 实现确定性布局算法**

  实现顺序必须固定：

  1. 用四方向洪泛找所有 Ground 区域。
  2. 每个区域按 `ceil(groundCells / GroundCellsPerStyleSeed)` 放 Style 种子；每一轮每个种子至多抢一个未占 Ground 邻格，直到区域填满。
  3. 一轮扫描把没有同 Style 2×2 支撑的尖端格改成最多相邻的 Style。
  4. 给每个 Ground 格写它的 Style 基础 RuleTile；给 Void/Obstacle 写单图规则格，角色是 `Abyss`、`VoidWall`、`VoidTransition`、`ObstacleTop`、`ObstacleWall`、`ObstacleTransition`。高度正向使用屏幕下方邻格 `(0, -1)` 判断暴露墙面：边界障碍格写 Wall，其余障碍格写 Top；直接位于其前方的 Ground 格写 Transition。Void 使用同样的前方方向，边缘 Void 格写 Wall，紧邻的 Ground 格写 Transition，其他 Void 格写 Abyss。
  5. 每条装饰根据所属区域面积除以 `πR²` 得到中心数；从随机中心以随机四邻扩张到最大次数，只接受 `IsStyleInterior` 的格，随后对正蔓延簇执行一轮 2×2 清理。
  6. 每种障碍物做加权候选、随机旋转/翻转、间距检查和占格检查。每个碰撞格必须为 Ground，八邻格为 Ground，不得命中 protectedCells 或已有障碍碰撞格。保存实际翻转/旋转后的 Sprite、视觉锚点与碰撞格。

  所有随机源由 `layout.Seed` 与稳定的 Style/定义索引混合得到，保证同一布局和配置产生同一视觉布局。

- [ ] **Step 4: 运行测试与编译**

  运行 Task 1 的 Unity EditMode 命令与 `dotnet build Assembly-CSharp.csproj -nologo --no-restore -v:q`。

  Expected: 所有 Ground 都有 Style；没有装饰接触 Style 边缘；所有障碍碰撞格通过八邻域和 protectedCells 检查。

- [ ] **Step 5: 提交**

  ```powershell
  git add Assets/Scripts/Game/OpenField/OpenFieldDungeonVisualLayoutBuilder.cs Assets/Tests/Editor/OpenFieldVisualThemeTests.cs
  git commit -m "feat: build open field visual layouts"
  ```

## Task 4: 场景数据、保留格与逐格碰撞

**Files:**

- Modify: `Assets/Scripts/Core/Runtime/RuntimeDataComponent.cs`
- Modify: `Assets/Scripts/Core/Runtime/OpenFieldDungeonSceneDataBuilder.cs`
- Modify: `Assets/Tests/Editor/OpenFieldVisualThemeTests.cs`

**Interfaces:**

- Produces `RuntimeDungeonTerrainVisualData TerrainVisual`，替换 `List<RuntimeDungeonTileSpawnData> TileSpawns`。
- Produces `List<RuntimeDungeonObstacleSpawnData> ObstacleSpawns`，每项含 Sprite 引用、世界坐标、排序锚点和 `List<Vector2Int> CollisionCells`。
- Consumes `OpenFieldDungeonVisualLayoutBuilder.Build(...)`。

- [ ] **Step 1: 写出失败的场景数据测试**

  使用 Task 3 的虚拟视觉配置和经过锚点/内容放置的 layout，断言：

  ```csharp
  RuntimeDungeonSceneData scene = OpenFieldDungeonSceneDataBuilder.Build(layout, theme, new DungeonConfig(), 1, false);
  Assert.That(scene.TerrainVisual.Placements.Count, Is.GreaterThan(0));
  Assert.That(scene.ObstacleSpawns.SelectMany(x => x.CollisionCells), Does.Not.Contain(new Vector2Int(layout.Entrance.X, layout.Entrance.Y)));
  ```

- [ ] **Step 2: 运行测试确认失败**

  Run: Task 1 的 Unity EditMode 命令。

  Expected: `TerrainVisual`、`ObstacleSpawns` 与新构建流程不存在。

- [ ] **Step 3: 更换场景数据载荷并接入既有内容**

  在 `RuntimeDataComponent.cs` 定义：

  ```csharp
  public enum RuntimeDungeonTilemapLayer { Void, Ground, Decoration, Obstacle }
  public sealed class RuntimeDungeonRuleTilePlacement
  {
      public RuntimeDungeonTilemapLayer Layer;
      public string RuleTilePath;
      public Vector2Int Cell;
      public int HeightSteps;
  }
  ```

  `OpenFieldDungeonSceneDataBuilder.Build` 必须先收集入口、出口、宝箱的 protectedCells，调用 Task 3 布局构建器，再将障碍碰撞格加入 `occupiedCells`，之后才调用既有 `AddLandmarks` 和 `AddSquads`。保留原来的 Void/Obstacle 地形矩形碰撞；每个障碍掩码格额外输出一个 `RuntimeDungeonObstacleSpawnData.CollisionCells` 项，绝不把整个视觉 footprint 当成碰撞。

- [ ] **Step 4: 运行测试与编译**

  运行 Task 1 的 Unity EditMode 命令与 `dotnet build Assembly-CSharp.csproj -nologo --no-restore -v:q`。

  Expected: 老的 `ResolveTerrainTile`、`RuntimeDungeonTileSpawnData` 和 `TileSpawns` 没有引用；出口、宝箱、怪物逻辑仍输出原有 spawn 数据。

- [ ] **Step 5: 提交**

  ```powershell
  git add Assets/Scripts/Core/Runtime/RuntimeDataComponent.cs Assets/Scripts/Core/Runtime/OpenFieldDungeonSceneDataBuilder.cs Assets/Tests/Editor/OpenFieldVisualThemeTests.cs
  git commit -m "feat: emit open field tilemap scene data"
  ```

## Task 5: 临时 RuleTile Tilemap 与 Back/Top 烘焙

**Files:**

- Create: `Assets/Scripts/Core/Runtime/DungeonRuleTileVisualBuilder.cs`
- Modify: `Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs`
- Modify: `Assets/Scripts/Core/Runtime/DungeonSceneRuntimeRoot.cs`
- Delete: `Assets/Scripts/Core/Runtime/DungeonTileVisualBuilder.cs` 和 `.meta`
- Modify: `Assets/Tests/Editor/OpenFieldVisualThemeTests.cs`

**Interfaces:**

- Produces `DungeonRuleTileVisualBuilder.Build(DungeonSceneRuntimeRoot runtimeRoot, RuntimeDungeonTerrainVisualData terrainVisual, string resourceOwnerKey)`。
- Consumes Task 4 的 `RuntimeDungeonRuleTilePlacement`；用 `ResourceComponent.Instance.Load<RuleTile>(path, ownerKey)` 取得 RuleTile。
- Produces两个运行时 MeshRenderer：`DungeonTerrainBack` 和 `DungeonTerrainTop`。
- Produces `RuntimeDungeonBakedLayers DungeonRuleTileBakeComposer.Compose(IReadOnlyList<ResolvedDungeonTileSprite> resolvedSprites, float cellWorldSize)`，供 EditMode 测试直接验证像素组合。

- [ ] **Step 1: 写出失败的烘焙合成测试**

  在 EditMode 测试内创建两个 `Sprite.Create` 的 16×16 测试 Sprite，以及一个 `RuleTile`，给 `m_DefaultSprite` 指定测试 Sprite。准备一条 Back placement 与一条高度为 2 的 Top placement，断言：

  ```csharp
  RuntimeDungeonBakedLayers layers = DungeonRuleTileBakeComposer.Compose(resolvedSprites, 1f);
  Assert.That(layers.BackTexture.width, Is.GreaterThan(16));
  Assert.That(layers.TopTexture.height, Is.GreaterThan(16));
  Assert.That(layers.TopWorldOffset.y, Is.GreaterThan(0f));
  ```

  测试只调用公开的无资源加载合成器 `DungeonRuleTileBakeComposer.Compose`，不依赖场景中的 `ResourceComponent`。

- [ ] **Step 2: 运行测试确认失败**

  Run: Task 1 的 Unity EditMode 命令。

  Expected: `DungeonRuleTileBakeComposer`、`ResolvedDungeonTileSprite`、`RuntimeDungeonBakedLayers` 不存在。

- [ ] **Step 3: 实现临时 Tilemap 解析与组合器**

  `DungeonRuleTileVisualBuilder` 必须：

  1. 建立仅在构建期间存在的 Grid，子节点顺序固定为 Void、Ground、Decoration、Obstacle 四张 Tilemap；Void 与 Obstacle 各只创建一张。
  2. 依据 `RuntimeDungeonTilemapLayer` 把 placement 的 RuleTile 写入对应 map，调用 `RefreshAllTiles()`，读取 `Tilemap.GetSprite(cell)`。
  3. 将 Void、Ground、Decoration 解析 Sprite 送入 Back；Obstacle 解析 Sprite 送入 Top。
  4. 在 `DungeonRuleTileBakeComposer.Compose` 中以每格半格高度的纵向偏移合成纹理：高度 `h` 的顶部向上偏移 `h * cellPixels / 2`，Void `-1` 向下半格，Obstacle Wall 对每一高度段复制墙图。
  5. 以合成纹理创建一个 quad Mesh、URP Sprite-Unlit 材质和 MeshRenderer；`DungeonTerrainTop` 的深度在角色前方，`DungeonTerrainBack` 在角色后方。
  6. 立即销毁临时 Grid/Tilemap；把纹理、Mesh 和材质交给 `DungeonSceneRuntimeRoot.TrackRuntimeAsset`，保证切场景时释放。

  不可调用 `TownTilemapBakeUtility`：它是编辑器资产导出工具，使用 `AssetDatabase`，不能进入运行时路径。

- [ ] **Step 4: 用新构建器替换旧入口**

  在 `DungeonSceneRuntimeBuilder.BuildCurrentDungeonSceneCoroutine` 以新 `DungeonRuleTileVisualBuilder.Build` 替换旧 `DungeonTileVisualBuilder.Build`。删除旧 `.cs/.meta` 后执行 `rg "DungeonTileVisualBuilder|TileSpawns" Assets`，结果必须为空。

- [ ] **Step 5: 运行测试与编译**

  运行 Task 1 的 Unity EditMode 命令与 `dotnet build Assembly-CSharp.csproj -nologo --no-restore -v:q`。

  Expected: RuleTile 使用其 Unity 解析出的 Sprite；Back/Top 各只有一个运行时 Renderer；临时 Tilemap 不残留在 `__DungeonRuntime` 下。

- [ ] **Step 6: 提交**

  ```powershell
  git add Assets/Scripts/Core/Runtime/DungeonRuleTileVisualBuilder.cs Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs Assets/Scripts/Core/Runtime/DungeonSceneRuntimeRoot.cs Assets/Tests/Editor/OpenFieldVisualThemeTests.cs
  git rm Assets/Scripts/Core/Runtime/DungeonTileVisualBuilder.cs Assets/Scripts/Core/Runtime/DungeonTileVisualBuilder.cs.meta
  git commit -m "feat: bake open field ruletile terrain"
  ```

## Task 6: 障碍 Sprite、逐格碰撞与排序

**Files:**

- Modify: `Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs`
- Modify: `Assets/Scripts/Core/Runtime/DungeonSceneVisualUtility.cs`
- Modify: `Assets/Tests/Editor/OpenFieldVisualThemeTests.cs`

**Interfaces:**

- Produces `DungeonSceneVisualUtility.ApplySpriteVisual(EntityManager entityManager, Entity entity, string spriteAssetPath, string spriteName, Vector4 spriteUv, string ownerKey, DungeonSceneRuntimeRoot runtimeRoot)`。
- Consumes `RuntimeDungeonObstacleSpawnData` from Task 4.
- Uses existing ECS Environment prefab name `Environment` for visible obstacles and `Collider` for every `CollisionCells` entry.

- [ ] **Step 1: 写出失败的障碍场景数据测试**

  在已有场景数据测试中加入：

  ```csharp
  RuntimeDungeonObstacleSpawnData obstacle = scene.ObstacleSpawns.Single();
  Assert.That(obstacle.CollisionCells.Count, Is.EqualTo(3));
  Assert.That(obstacle.SortAnchorWorldY, Is.EqualTo(obstacle.WorldPosition.y + obstacle.VisualSortAnchor.y).Within(0.001f));
  ```

- [ ] **Step 2: 运行测试确认失败**

  Run: Task 1 的 Unity EditMode 命令。

  Expected: `SortAnchorWorldY` 或逐格碰撞数据尚未生成。

- [ ] **Step 3: 用已有 ECS Environment 预制体生成障碍**

  在 `DungeonSceneRuntimeBuilder` 新增 `SpawnObstacles`：每个障碍使用 `TryInstantiateEnvironment(..., "Environment", ...)` 生成一个可见实体；每个 collision cell 使用 `TryInstantiateEnvironment(..., "Collider", ...)` 生成一个 `1×1×1.6` 的隐藏碰撞实体。两类实体都加入 `spawnedEntities`，因此 `DungeonSceneRuntimeRoot` 销毁时会释放。

  在 `DungeonSceneVisualUtility.ApplySpriteVisual` 使用 `ResourceComponent.LoadSprite($"{spriteAssetPath}|{spriteName}", ownerKey)` 读取 Sprite，按 sprite rect 生成 quad Mesh 与 Sprite-Unlit 材质，调用 `ApplySharedMaterial`。新 Mesh 和 Material 交由 `runtimeRoot.TrackRuntimeAsset`。实体 `LocalTransform.Position.z` 设为 `-SortAnchorWorldY / 100f`，使较低的视觉锚点绘制在较高的锚点前；这与角色现有的 `-Position.y * 100` SpriteRenderer 排序方向一致。

- [ ] **Step 4: 运行测试与编译**

  运行 Task 1 的 Unity EditMode 命令与 `dotnet build Assembly-CSharp.csproj -nologo --no-restore -v:q`。

  Expected: 可见障碍仅使用拖入 Sprite；每个 true 碰撞掩码格正好一个 Collider 实体；非碰撞视觉格不产生 collider。

- [ ] **Step 5: 提交**

  ```powershell
  git add Assets/Scripts/Core/Runtime/DungeonSceneRuntimeBuilder.cs Assets/Scripts/Core/Runtime/DungeonSceneVisualUtility.cs Assets/Tests/Editor/OpenFieldVisualThemeTests.cs
  git commit -m "feat: spawn open field sprite obstacles"
  ```

## Task 7: 清除旧 MapTest 缓存并进行端到端验证

**Files:**

- Modify: `Assets/Scripts/Game/MapDemo/OpenFieldMapTestDemo.cs`
- Modify: `Assets/Scripts/Game/MapDemo/Editor/OpenFieldMapTestDemoEditor.cs`
- Delete: `Assets/Scripts/Game/MapDemo/OpenFieldMapPlayDemo.cs` 和 `.meta`（当前为未追踪文件）
- Delete: `Assets/Resources/MapTest/LatestHeightMap.png` 和 `.meta`
- Modify: `Assets/Scenes/MapTest.unity`
- Modify: `Assets/Tests/Editor/OpenFieldVisualThemeTests.cs`

**Interfaces:**

- MapTest 不再公开或读取 `LatestHeightMap` 资源路径。
- MapTest 仅保留内存中的生成预览；其场景中不再挂接 `OpenFieldMapPlayDemo`。

- [ ] **Step 1: 写出失败的无缓存测试**

  在测试中加入：

  ```csharp
  [Test]
  public void LegacyMapTestHeightCache_DoesNotExist()
  {
      Assert.That(File.Exists("Assets/Resources/MapTest/LatestHeightMap.png"), Is.False);
  }
  ```

- [ ] **Step 2: 运行测试确认失败**

  Run: Task 1 的 Unity EditMode 命令。

  Expected: `LatestHeightMap.png` 仍存在。

- [ ] **Step 3: 移除旧试玩实现与缓存行为**

  从 `OpenFieldMapTestDemo` 删除 `LatestMapResourcesPath`、`LatestMapAssetPath`、编码/写盘/加载缓存的方法及其调用。`Start` 在没有内存地图时调用现有 `GenerateDemo()`，不读取文件。编辑器按钮改名为 `Generate Map`，删除“下次 Play 会读取保存图片”的 HelpBox。移除 MapTest 场景里的 `OpenFieldMapPlayDemo` 组件，然后删除该脚本与 meta。最后删除经路径确认的 PNG 缓存和 meta。当前这四个目标都是未追踪文件，使用下列精确 `Remove-Item -LiteralPath` 命令而不是 `git rm`：

  ```powershell
  Remove-Item -LiteralPath 'E:\Unity\Project\Crystal Magic\Assets\Scripts\Game\MapDemo\OpenFieldMapPlayDemo.cs','E:\Unity\Project\Crystal Magic\Assets\Scripts\Game\MapDemo\OpenFieldMapPlayDemo.cs.meta','E:\Unity\Project\Crystal Magic\Assets\Resources\MapTest\LatestHeightMap.png','E:\Unity\Project\Crystal Magic\Assets\Resources\MapTest\LatestHeightMap.png.meta' -Force
  ```

- [ ] **Step 4: 运行完整验证**

  依次运行：

  ```powershell
  & 'E:\Unity\Editor\6000.1.17f1\Editor\Unity.exe' -batchmode -quit -projectPath 'E:\Unity\Project\Crystal Magic' -runTests -testPlatform EditMode -testResults 'Temp\OpenFieldVisualThemeTests.xml'
  dotnet build Assembly-CSharp.csproj -nologo --no-restore -v:q
  git diff --check
  ```

  然后在 Unity 手工验证：

  1. 打开 Dungeon Editor，确认主题表为空且可创建新主题。
  2. 新建一条测试主题，给六张地形 RuleTile、两种 Ground Style、每种一条装饰和一条障碍配置测试资产。
  3. 进入地牢，确认临时 Tilemap 不留在层级中，Back/Top 各一层，角色可以走 Ground、被 Void/Obstacle/障碍掩码阻挡。
  4. 在障碍前后移动，确认排序锚点使角色前后遮挡正确。
  5. 删除测试主题再次进入，确认获得已有的“没有配置主题”的明确错误，而不是旧 Cave squad 错误。

- [ ] **Step 5: 请求代码审查并更新审查状态**

  对本功能的全部 `.cs` 变更调用 `superpowers:requesting-code-review`。按项目约定检查 `E:\workspace\TestPro\Crystal-Magic\cs_review_status.txt`：若该路径在执行环境中存在，已审查的文件从 `FALSE` 标为 `TRUE`；若不存在，在交付说明中明确记录其缺失，不能伪造状态文件。

- [ ] **Step 6: 提交**

  ```powershell
  git add Assets/Scripts/Game/MapDemo/OpenFieldMapTestDemo.cs Assets/Scripts/Game/MapDemo/Editor/OpenFieldMapTestDemoEditor.cs Assets/Scenes/MapTest.unity Assets/Tests/Editor/OpenFieldVisualThemeTests.cs
  git commit -m "refactor: remove legacy open field map demo"
  ```
