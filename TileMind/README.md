# TileMind — 日麻 AI 辅助工具

基于 .NET 10 + ONNX Runtime 的实时日麻对局分析工具，支持屏幕捕获、YOLO 牌识别、牌型分析与 WPF 叠加层显示。

## 架构概览

```
┌─────────────────────────────────────────────────────────┐
│                       TileMind.UI                        │
│              WPF 桌面应用 / Overlay 叠加层 / 设置页面       │
├─────────────────────────────────────────────────────────┤
│                      TileMind.Core                       │
│     DI 注册 / 静态分析 / 牌型分析 / 对局状态追踪 / 动作分类   │
├──────────────┬──────────────┬──────────────┬─────────────┤
│ TileMind.AI  │ TileMind.Vision │ TileMind.Common │ TileMind.Algorithm │
│  AI 决策     │ DXGI 捕获       │ 共享模型、配置   │ RiichiSharp 适配层 │
│  (占位)      │ YOLO 推理    │ 工具类、扩展方法  │ 向听/听牌/得点计算  │
│              │ 多帧融合        │ 显示器枚举        │                    │
│              │ 显示器枚举      │                  │                    │
└──────────────┴──────────────┴──────────────┴─────────────┘
```

## 模块说明

| 模块 | 功能 |
|------|------|
| **TileMind.Common** | 共享数据模型（`TileType`, `DetectionResult`, `GameState`, `TileAnalysisResult`, `MonitorInfo` 等）、配置选项、几何工具（`GeometryHelper`）、游戏窗口定位（`WindowFinderHelper`） |
| **TileMind.Algorithm** | **RiichiSharp 适配层** — 牌型映射（`TileTypeMapper`）、手牌格式转换（`HandStringBuilder`）、牌型分析服务（向听数、听牌判定、打牌推荐、胡牌得点、牌剩余统计） |
| **TileMind.Core** | 依赖注入胶水层、**静态分析**（手牌/副露分离、副露类型判定、暗杠推断、宝牌映射、立直检测）、**牌型分析**（调用 RiichiSharp）、**对局状态追踪**（可选：跨帧 IoU 匹配、动作分类） |
| **TileMind.Vision** | DXGI 桌面复制 API 屏幕捕获、YOLO ONNX 推理（GPU CUDA / CPU 回退）、多帧融合（加权投票 + 场景变化检测）、显示器枚举服务（`MonitorEnumerator` / `MonitorService`） |
| **TileMind.AI** | AI 决策模块占位 |
| **TileMind.UI** | WPF-UI 桌面应用、透明 Overlay 叠加层（识别框/区域标记/耗时统计/牌型分析/剩余牌，支持跨屏坐标映射）、屏幕区域标定工具（`ScreenSplitterWindow`）、导航/设置页面 |
| **TileMind.Console** | 控制台测试入口 |

## 核心流程

```
屏幕捕获 (DXGI, OutputIndex 指定显示器)
    → YOLO 推理 (ONNX Runtime, GPU)
    → 多帧融合 (加权投票, 场景变化检测)
    → 区域路由 (按 ScreenCaptureOptions 派生区域分发至各玩家/区域)
    → 静态分析 (FrameAnalyzerService: 手牌/副露分离, 副露类型, 宝牌, 立直)
    → 牌型分析 (TileAnalysisService: 向听数, 牌剩余, 打牌推荐/胡牌得点)
    ├─→ Stage 1 UI 叠加层: 识别框 + 区域标记 + 耗时统计 + 牌型分析 + 牌剩余 (每帧)
    └─→ [可选] 状态追踪 (GameStateTracker: 帧间匹配, 动作分类)
         └─→ Stage 2 UI: 动作日志 (仅追踪模式)
```

## 快速开始

### 环境要求

- Windows 10+（DXGI 桌面复制需要）
- .NET 10 SDK
- NVIDIA GPU + CUDA 12+（可选，CPU 回退可用）
- ONNX Runtime GPU 依赖（`Dependency/` 目录下）

### 构建

```bash
dotnet build
```

### 运行

```bash
dotnet run --project TileMind.UI        # WPF 桌面应用
dotnet run --project TileMind.Console   # 控制台测试
```

## 配置

所有配置项通过 JSON 文件管理（`settings/` 目录）：

| 文件 | 内容 |
|------|------|
| `screencapturesettings.json` | 截取显示器编号（`OutputIndex` / `AdapterIndex`）、游戏进程名、区域比值坐标（`Ratio`，0~1 相对屏幕或游戏窗口，运行时解析为绝对像素） |
| `yolosettings.json` | 模型路径、置信度/IoU 阈值、GPU 设备 ID、输入尺寸、检测器池大小 |
| `framefusionsettings.json` | 融合帧数、变化阈值、融合置信度/IoU 阈值 |
| `gamestatetrackersettings.json` | 追踪 IoU 阈值、miss 容限、Gap 倍数、聚类容差、邻近阈值 |
| `overlaysettings.json` | 覆盖层显示器编号（`OutputIndex`）、功能开关（识别框/区域/耗时/牌型分析/剩余牌/动作日志/AI决策）、各显示项位置比值与对齐方式 |
| `pipelinesettings.json` | 流水线行为（状态追踪启用/关闭） |

## 显示器管理

启动时通过 SharpDX DXGI 枚举所有适配器和显示器输出。`OutputIndex` 对应 DXGI 输出索引（0-based），与 `AdapterIndex` 组合唯一标识一个显示器。

- `ScreenCaptureOptions.OutputIndex` — 屏幕捕获目标显示器
- `OverlayOptions.OutputIndex` — 覆盖层显示目标显示器

两者可配置为不同显示器，覆盖层会自动将截取屏坐标映射到显示屏。

## 项目依赖

- **OpenCvSharp4** — 图像处理
- **Microsoft.ML.OnnxRuntime** — YOLO 推理
- **SharpDX** — 高性能屏幕捕获 + 显示器枚举
- **WPF-UI** — Fluent Design 桌面界面
- **CommunityToolkit.Mvvm** — MVVM 工具包
- **ZLogger** — 结构化日志

## 目录结构

```
TileMind/
├── TileMind.Common/        # 共享层
│   ├── Config/             #   配置选项类 (ScreenCaptureOptions, OverlayOptions 等)
│   ├── Helpers/            #   工具类 (GeometryHelper, WindowFinderHelper, SettingConfigExtensions)
│   ├── Logging/            #   日志配置
│   └── Models/             #   数据模型 (DetectionResult, TileType, GameState, MonitorInfo 等)
├── TileMind.Algorithm/     # 算法适配层
│   └── RiichiSharp 桥接 / TileTypeMapper / HandStringBuilder / TileAnalysisService
├── TileMind.Core/          # 核心层
│   └── Services/           #   DI 注册 / FrameAnalyzerService / GameStateTracker / ActionClassifier
├── TileMind.Vision/        # 视觉层
│   ├── Detection/          #   YOLO 检测器 / 对象池
│   └── ScreenCapture/      #   DXGI 捕获 / 帧融合 / 显示器枚举
├── TileMind.AI/            # AI 决策 (占位)
├── TileMind.UI/            # WPF 桌面应用
│   ├── Converters/         #   值转换器
│   ├── Overlay/            #   叠加层绘制系统 (DrawingCommand / OverlayBaseControl)
│   ├── ViewModels/         #   视图模型 (OverlayWindow / Settings / ScreenSplitter)
│   ├── Views/              #   页面/窗口
│   └── Services/           #   应用托管
├── TileMind.Console/       # 控制台测试
└── Dependency/             # 原生依赖 (cuDNN 等)
```

## 当前状态

- **覆盖层绘制**：✅ 识别框、区域标记、耗时统计、牌型分析、牌剩余、跨屏坐标映射、鼠标穿透、高 DPI 支持
- **屏幕捕获**：✅ DXGI 桌面复制、YOLO GPU 推理（CUDA）、多帧融合
- **静态分析**：✅ 手牌/副露分离、副露类型判定、暗杠推断、宝牌映射、立直检测
- **牌型分析**：✅ 已集成 RiichiSharp（向听数、听牌判定、打牌推荐、胡牌得点、牌剩余统计）
- **状态追踪**：🔧 架构就绪（基线建立、帧间 IoU 匹配、动作分类），实际对局精度待调优
- **显示器管理**：✅ SharpDX 枚举、跨屏坐标映射、覆盖层窗口定位
- **区域标定**：✅ ScreenSplitter 四边形拖拽工具，Ratio 坐标保存/重定位

### 示例-第一阶段-基础识别与覆盖层绘制

![程序运行示例](Docs/Images/基础识别与覆盖层绘制.png)

### 示例-第二阶段-完整静态分析功能实现

参照分屏功能-副屏绘制功能

![程序运行示例](Docs/Images/分屏功能-副屏绘制.png)

### 示例-第三阶段-分屏功能

截取屏与覆盖屏可配置为不同显示器，覆盖层自动完成坐标映射，在主屏游戏时副屏同步显示识别结果。

<table><tr><td><img src="Docs/Images/分屏功能-主屏游戏.png" alt="主屏"/></td><td><img src="Docs/Images/分屏功能-副屏绘制.png" alt="副屏"/></td></tr></table>

## 待办

### 调试与调优

- **立直检测** — 弃牌区宽高比判定待实局验证，接入 `GameStateTracker`
- **副露检测** — `HandMeldSeparator`/`DetermineMeldType`/`InferAnkans` 精度调优

### 新功能

- **InfoArea OCR** — 集成 ONNX OCR 模型识别场风、自风、分数、场信息
- **一发/振听判定** — 接入 RiichiSharp 的 `IsIppatsu` 和 `FuritenDetector`
- **调试录制与回放** — 录制每帧截图+检测+分析结果，人工标注后对比差异
- **牌堆剩余牌显示优化** — 按花色分组、高亮、精简显示

### 持续

- **多操作帧分析**（5b/5c）
- **副露触发牌位置** — 吃/碰组中横置牌的精确定位
- **状态追踪调优** — 动作分类规则实局准确度验证
- **对局记录导出**（牌谱格式）
- **单元测试**

### 性能优化

- YOLO 输入 Tensor 预分配（复用 Buffer.Span）
- ONNX 输出缓冲复用（`OrtValue` 原生缓冲）
- OpenCV Mat 对象池
- unsafe Span 零拷贝访问 Mat 数据
