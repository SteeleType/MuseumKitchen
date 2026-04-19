# MuseumKitchen — 项目结构分析

> 一个 Unity 6 博物馆装置/展览项目：观众在平板上随机生成"饺子组合"，通过局域网 UDP 广播提交到大屏，由大屏实时拼贴出全场观众的"集体料理"卡片墙；并以"香料贸易"为切入点，串联全球菜品的食材—产地—路线叙事。

---

## 一、项目概况

| 项 | 值 |
|---|---|
| 项目名 | **MuseumKitchen** |
| Unity 版本 | **6000.3.7f1** |
| 渲染管线 | URP 17.3.0 |
| 目标平台 | StandaloneWindows64 |
| 输入系统 | Input System 1.18 (`InputSystem_Actions.inputactions`) |
| UI | uGUI + TextMesh Pro |
| 动画库 | **DOTween**（位于 `Assets/Plugins/Demigiant`） |
| 集成工具 | **MCPForUnity**（本地内嵌包，供 Claude/MCP 操作编辑器） |
| 仓库 | github.com/SteeleType/MuseumKitchen |

---

## 二、目录结构（Assets/）

```
Assets/
├── Animation/               动画控制器与剪辑（Plane, RevealDish, RevealObjects）
├── Art/                     原画/图片素材（地图、食材、按钮、UI 背景）
├── Editor/                  编辑器扩展
│   └── AddBigScreenToBuild.cs   一键把 BigScreen 场景加入 Build Settings
├── Font/                    字体
├── MCPForUnity/             MCP 集成包（编辑器+运行时）
├── Plugins/Demigiant/       DOTween
├── Prefabs/
│   └── SpiceManager.prefab
├── Resources/
│   └── DOTweenSettings.asset
├── Scenes/                  五个场景，详见下表
├── Scripts/                 玩法、网络、SO、系统脚本
├── Settings/                URP 渲染设置
├── TextMesh Pro/            TMP 默认资源
├── DefaultVolumeProfile.asset
├── InputSystem_Actions.inputactions
└── UniversalRenderPipelineGlobalSettings.asset
```

---

## 三、场景一览

`ProjectSettings/EditorBuildSettings.asset` 中已加入 Build 的场景：

| # | 场景 | 角色 | 说明 |
|---|---|---|---|
| 0 | [Kitchen.unity](Assets/Scenes/Kitchen.unity) | 客户端（平板）主玩法场景 | 玩家随机抽食材 → 提交饺子组合 |
| 1 | [StartScene.unity](Assets/Scenes/StartScene.unity) | 启动/标题场景 | — |
| 2 | [BigScreen.unity](Assets/Scenes/BigScreen.unity) | 大屏 Host | 接收 LAN 数据，拼贴卡片墙 |

仅在工程中存在（暂未加入 Build）：

| 场景 | 角色 |
|---|---|
| [SpiceTradeMap.unity](Assets/Scenes/SpiceTradeMap.unity) | 香料贸易路线地图（叙事/可视化） |
| [DishReveal.unity](Assets/Scenes/DishReveal.unity) | 菜品揭示动画 |

---

## 四、核心系统

整体架构是 **客户端（平板，多台）  ──UDP 广播──>  大屏（Host）** 的局域网装置。

```
┌─────────── Tablet (Client) ───────────┐         ┌──────── Big Screen (Host) ────────┐
│                                       │         │                                   │
│ RandomIngredient                      │         │ MuseumLanReceiver                 │
│   ├─ 抽取 Filling/Wrapping/Cooking    │         │   ├─ 后台线程 UDP 监听            │
│   └─ SubmitPotluck()                  │  UDP    │   ├─ 主线程 dequeue → JSON parse  │
│        │                              │ ──────> │   └─ Invoke OnDataReceived        │
│ MuseumLanSender                       │  9001   │                                   │
│   └─ JsonUtility → Broadcast          │         │ PotluckManager                    │
│                                       │         │   ├─ Grid 排布卡片                │
│ NetworkUI (Client mode)               │         │   ├─ 超过 maxCards → Fade 最旧    │
│   └─ 显示本机 IP / 输入目标 IP        │         │   └─ 实例化 DishCardUI            │
│                                       │         │                                   │
└───────────────────────────────────────┘         │ DishCardUI                        │
                                                  │   └─ DOTween 入场+定时淡出        │
                                                  │                                   │
                                                  │ NetworkUI (Host mode)             │
                                                  │   └─ 仅显示本机 IP                │
                                                  └───────────────────────────────────┘
```

### 1. 网络层（[Assets/Scripts/Network/](Assets/Scripts/Network/)）

| 文件 | 职责 |
|---|---|
| [PotluckData.cs](Assets/Scripts/Network/PotluckData.cs) | 网络消息体（`clientId / fillingName / wrappingName / cookingMethodName`），`[Serializable]`，走 `JsonUtility` |
| [MuseumLanSender.cs](Assets/Scripts/Network/MuseumLanSender.cs) | 客户端发送器：UDP 广播（默认 `255.255.255.255:9001`），自动写入 `SystemInfo.deviceName` 作为 clientId |
| [MuseumLanReceiver.cs](Assets/Scripts/Network/MuseumLanReceiver.cs) | 大屏接收器：后台线程 `UdpClient.Receive` → `ConcurrentQueue` → 主线程 `Update` 中反序列化并 `Invoke` 事件 / 自动转发到同 GameObject 上的 `PotluckManager` |
| [DishCardUI.cs](Assets/Scripts/Network/DishCardUI.cs) | 单张卡片 UI，运行时构建（Image + VerticalLayoutGroup + 4 行 TMP），DOTween 入场/淡出 |
| [NetworkUI.cs](Assets/Scripts/Network/NetworkUI.cs) | 运行时构建的网络面板：Host 仅显示本机 IP；Client 额外提供"目标 IP"输入框 |

### 2. 大屏展示层（[Assets/Scripts/PotluckManager.cs](Assets/Scripts/PotluckManager.cs)）

- 在 `Awake` 中查找 Canvas 并铺一个全屏的 `DishCardContainer`。
- `OnPotluckDataReceived` 收到一份数据：超过 `maxCards`（默认 12）时移除最旧并 `RepositionCards`，否则在网格下一格生成新卡。
- 网格参数（列数 / cardSize / spacing / 偏移 / lifetime）全部在 Inspector 暴露。

### 3. 客户端玩法（[Assets/Scripts/RandomIngredient.cs](Assets/Scripts/RandomIngredient.cs)）

- `[RequireComponent(typeof(MuseumLanSender))]`，与发送器强绑定。
- 三个独立按钮分别"重抽"馅料 / 面皮 / 烹饪方式（更新 TMP 文本与 `SpriteRenderer` 上的 sprite）。
- `SubmitPotluck()` 校验三项齐全后构建 `PotluckData` 调用 `lanSender.SendDumplingData(data)`。

### 4. ScriptableObject 数据层

| 路径 | 作用 |
|---|---|
| [Ingredient Scriptable Obejects/](Assets/Scripts/Ingredient%20Scriptable%20Obejects/) | `Ingredient` SO（name / sprite / IngredientType=Filling/Wrapping/CookingMethod）。已配资产：Ground Pork、Ground Chicken、Mushroom、Wheat Dough、Thin Wheat、Boiled、Pan-Fried |
| [Dumpling Scriptable Objects/](Assets/Scripts/Dumpling%20Scriptable%20Objects/) | `Dumpling` SO（filling / wrapping / cookingMethod 三个 Ingredient 引用）。资产：Jiaozi、Gyoza |
| [Potluck Scriptable Objects/](Assets/Scripts/Potluck%20Scriptable%20Objects/) | `Dish` SO + 三组枚举 + 静态 `SpiceManager`，详见下 |
| [Potluck Scriptable Objects/SOs/](Assets/Scripts/Potluck%20Scriptable%20Objects/SOs/) | **30+ 全球菜品 SO**：Pho、Biryani、Paella Mixta、Tagine、Risotto alla Curcuma、Saffron Rice、Steak Poivre、Goulash、Chicken Adobo、Mujaddara、Pastilla、Tahdig、Kheer、Kabob Koobideh、Cacio e Pepe、Shepherd's Pie、Samosa、Chelsea Buns、Kardemummabullar、Carrot Cake … |

`Dish.cs` 字段：`dishName / dishSprite / countryOfOrigin / Region / Spice / CookingMethod / distanceTraveled / SpiceOrigin`，并在 `OnEnable` 通过 `SpiceManager.AddSpiceOrigin(spice)` 反查香料原产地。

枚举一览：

```csharp
enum Spice        { BlackPepper, Cardomom, Cinnamon, Cloves, Cumin, Saffron, Turmeric }
enum SpiceOrigin  { India, SriLanka, Iran, Indonesia, Mediterranean }
enum Region       { Europe, Asia, SouthAsia, MiddleEast, NorthAfrica }
enum CookingMethod{ Oven, Pot, Pan }
```

`SpiceManager`（静态）固定映射规则：BlackPepper/Cardomom/Turmeric→India、Cinnamon→SriLanka、Cloves→Indonesia、Cumin→Mediterranean、Saffron→Iran。

### 5. 地图/动画（[Assets/Scripts/Map Animations/](Assets/Scripts/Map%20Animations/)）

- [MapRenderer.cs](Assets/Scripts/Map%20Animations/MapRenderer.cs)：当前是占位实现 —— `Start` 里临时拉一条 3 顶点 LineRenderer，`MoveMaps()` 还是空方法。预期承担 SpiceTradeMap 场景中"贸易路线动画"的绘制。

### 6. 系统/场景流转（[Assets/Scripts/System/](Assets/Scripts/System/)）

- [SceneChange.cs](Assets/Scripts/System/SceneChange.cs)：极简包装 `SceneManager.LoadScene(sceneToLoad)`，挂在按钮上即可跳场景。

### 7. 编辑器扩展（[Assets/Editor/](Assets/Editor/)）

- [AddBigScreenToBuild.cs](Assets/Editor/AddBigScreenToBuild.cs)：菜单 `Tools > Add BigScreen to Build Settings`，把 BigScreen 场景写入 EditorBuildSettings（一次性脚本，已生效）。

---

## 五、关键依赖（[Packages/manifest.json](Packages/manifest.json)）

| 包 | 版本 | 用途 |
|---|---|---|
| `com.unity.render-pipelines.universal` | 17.3.0 | URP |
| `com.unity.inputsystem` | 1.18.0 | 新输入系统 |
| `com.unity.feature.2d` | 2.0.2 | 2D 工具集 |
| `com.unity.ugui` | 2.0.0 | uGUI |
| `com.unity.timeline` | 1.8.10 | Timeline |
| `com.unity.visualscripting` | 1.9.9 | Visual Scripting |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | JSON（注意：当前网络层用的是 `JsonUtility`，未使用 Newtonsoft） |
| `com.unity.test-framework` | 1.6.0 | 测试 |
| `com.unity.multiplayer.center` | 1.0.1 | （MP 工具，目前无 Netcode 依赖；联机走自实现 UDP） |
| `com.antigravity.ide` | git | IDE 集成 |

第三方资源：DOTween（`Assets/Plugins/Demigiant`），MCPForUnity 本地包（`Assets/MCPForUnity`，含独立 `package.json`）。

---

## 六、运行流程（人话版）

1. **大屏 PC** 打开 `BigScreen.unity` → `MuseumLanReceiver` 在 9001 端口监听；`NetworkUI(Host)` 显示本机 IP 供观众识别。
2. **每台平板** 打开 `Kitchen.unity` → 玩家点三个按钮分别抽 Filling / Wrapper / Cooking → 点"提交"调用 `RandomIngredient.SubmitPotluck()`。
3. `MuseumLanSender` 把 `PotluckData` 序列化为 JSON，UDP 广播到 `255.255.255.255:9001`（也可在 `NetworkUI(Client)` 输入指定 IP）。
4. 大屏接收线程入队 → 主线程反序列化 → `PotluckManager.OnPotluckDataReceived` 在网格下一格生成 `DishCardUI`，DOTween 弹入动画；超过 12 张时最旧卡淡出并整体 reflow。
5. 叙事场景（`SpiceTradeMap` / `DishReveal`）使用 SOs 中的 30+ 菜品数据，配合香料→原产地映射，做"全球饮食通过香料贸易彼此连接"的可视化。

---

## 七、当前状态 / TODO 观察

- ✅ 网络收发链路已打通，含线程安全队列、自动广播、IP UI。
- ✅ 大屏卡片墙具备完整动画与生命周期管理。
- ✅ ScriptableObject 数据库已配大量菜品，准备做叙事场景。
- 🟡 `MapRenderer.cs` 仍是占位 LineRenderer，`MoveMaps()` 未实现 —— SpiceTradeMap 路线动画待开发。
- 🟡 `Dish.cs` 中 `OnEnable` 调 `SpiceManager.AddSpiceOrigin(spice)` 时，若 SO 的 `spice` 字段未赋值会进入 `default` 抛异常；启动时所有 Dish SO 都会触发 `OnEnable`，建议加判空或 try/catch 防御。
- 🟡 `MuseumLanReceiver.OnDisable` 中调用了已被官方标注弃用的 `Thread.Abort()`，未来 .NET 升级或更换平台时需要替换为基于 `CancellationToken` 的协作式停止。
- 🟡 `NewBehaviourScript`/调试遗留较少，但 `Ingredient Scriptable Obejects` 目录拼写有误（`Obejects`）—— 改名要同步更新所有 .meta 引用，谨慎操作。
