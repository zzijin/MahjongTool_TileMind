# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build                    # Build entire solution
dotnet run --project TileMind.Console   # Console test harness
dotnet run --project TileMind.UI        # WPF desktop app
```

No test project exists yet. The solution uses .NET 10, Windows-only (WPF + DXGI).

**Current status**: Overlay drawing (detection boxes, screen regions, timing stats) is verified and working. Tile analysis is integrated via RiichiSharp (shanten/tenpai/scoring). Static analysis and state tracking need further debugging — the architecture is in place but classification accuracy requires tuning.

## Architecture

TileMind is a real-time mahjong AI assistant. Seven projects in a pipeline:

```
TileMind.Vision → TileMind.Core → TileMind.UI
       ↓                ↓
  TileMind.Common   TileMind.AI (placeholder)
```

- **TileMind.Common** — Shared types (`TileType` enum, `DetectionResult`, config options, game state models, tile analysis models), geometry helpers (`GeometryHelper` — ray-segment intersection for region computation), JSON config load/save (`SettingConfigExtensions`). Everything else depends on this.
- **TileMind.Algorithm** — Mahjong algorithm adapter layer. Bridges TileMind data models to RiichiSharp library. Contains `TileTypeMapper`, `HandStringBuilder`, `TileAnalysisService`. Produces `TileAnalysisResult` (shanten, tenpai, discard options, win options with scoring).
- **TileMind.Vision** — Screen capture (DXGI Desktop Duplication via SharpDX), YOLOv8 ONNX inference (GPU via CUDA, CPU fallback), multi-frame fusion.
- **TileMind.Core** — DI wiring (`ServiceExtensions`), multi-stage pipeline (`GamePipelineService` → `FrameAnalyzerService` → `TileAnalysisService` → `GameRecorderService` → `GameStateTracker`), static frame analysis (hand/meld separation, meld type classification, dora indicator mapping), tile analysis (shanten/tenpai/scoring), IoU-based frame-to-frame tile matching, action classification.
- **TileMind.UI** — WPF desktop app using WPF-UI library (Fluent Design). Transparent overlay window for drawing detection results, navigation pages (Home, Settings), tray icon.
- **TileMind.AI** — Empty project, reserved for future AI decision logic.

## Key Pipeline

`GamePipelineService.ProcessFrame()` is the main entry point:

1. `FrameFusionService.ProcessFrameFusion()` — captures N frames, runs YOLO on each, fuses by weighted voting. Cached when no scene change detected.
2. `RouteDetections()` — assigns each detection to a player/region by testing whether its center falls inside the quadrilateral regions defined in `ScreenCaptureOptions`. The 8 per-player regions (4× hand+meld + 4× discard pond) are auto-computed from 4 base regions by `ScreenCaptureOptions.ComputeDerivedAreas()`.
3. `FrameAnalyzerService.Analyze(FrameDetections)` — **static single-frame analysis** (no history needed). Runs `HandMeldSeparator` to split hand tiles from meld groups, determines meld types (Chi/Pon/Kan), maps dora indicators to actual dora tiles, detects riichi. Outputs `AnalyzedFrame`. This step runs regardless of tracking mode.
4. **Stage 1 UI publish** — `FrameStateHub.PublishAnalysis(AnalyzedFrame)` fires every frame, tracking or not. Overlay draws detection boxes and screen regions.
5. `TileAnalysisService.Analyze(AnalyzedFrame)` — **tile analysis** (every frame). Calls RiichiSharp to compute shanten number, remaining tile counts, discard efficiency (if not tenpai) or win options with scoring (if tenpai). Outputs `TileAnalysisResult` published via `FrameStateHub.PublishTileAnalysis()`.
6. `GameRecorderService.ProcessFrame(AnalyzedFrame)` — **state tracking** (optional, gated by `PipelineOptions.EnableStateTracking`). Feeds into `GameStateTracker` which handles:
   - Baseline establishment / initial deal detection (all ponds+meld empty → generate initial Draw actions)
   - `TileTracker` — IoU-based greedy tile matching across frames
   - `DetectNewMelds` / `DetectKakan` — cross-frame comparison against existing `MeldRecords`
   - `ActionClassifier` — per-player hand/pond diffs → Draw/Discard/Chi/Pon/Kan/Ankan/Kakan
7. **Stage 2 UI publish** — `FrameStateHub.PublishActions(List<MahjongAction>)` fires only in tracking mode.

### State Tracking Details

**Baseline**: Established on first frame OR when board is cleared (all discard ponds + melds empty, hands present). `IsBoardCleared()` triggers automatic re-establishment to catch round transitions.

**Initial deal detection**: When baseline is established and all players have 0 discards + 0 melds → generate Draw actions (13 tiles per player, dealer gets extra 14th). Dealer is determined as the player with 14 hand tiles.

**Absent players**: Seats with no detections in any area are skipped — no `PlayerState` is created. `ProcessFrame()` only iterates `_state.Players.Keys`, not all 4 `SeatPosition` values. No player count or seat wind validation.

## Coordinate Systems

Two distinct `Point` / `Rect` types coexist — be explicit about which is which:

| Namespace | Used in |
|-----------|---------|
| `OpenCvSharp` (int-based) | Detection results, screen capture, region quadrilaterals |
| `System.Windows` (double-based) | WPF overlay drawing, `ImageCoordinateHelper` |

`CommandExtensions.cs` provides `ToWRect()` / `ToMRect()` extension methods to convert between them.

## C# 13 Extension Syntax

The project uses C# 13 `extension()` blocks for extension methods, not traditional `static class` + `this`:

```csharp
// In ServiceExtensions.cs:
extension(IServiceCollection services)
{
    public void AddBaseServices() { ... }  // Called as services.AddBaseServices()
}

// In CommandExtensions.cs:
extension(OpenCvSharp.Rect rect)
{
    public System.Windows.Rect ToWRect() { ... }
}
```

Use this pattern for any new extension methods.

## Configuration

Settings are loaded from JSON files in a `settings/` directory relative to the working directory. Each module has its own file (e.g., `yolosettings.json`, `screencapturesettings.json`). Config classes live in `TileMind.Common.Config/` and expose `const string SettingFilePath`.

`ScreenCaptureOptions` defines 4 user-configured base regions (`TableArea`, `DiscardPondArea`, `DoraIndicatorArea`, `InfoArea`, each as `Point[4]`) plus `AdapterIndex`/`OutputIndex`. From these, 8 per-player derived regions (4× hand+meld + 4× discard pond) are auto-computed by `ComputeDerivedAreas()` using pure geometry (`GeometryHelper`). Derived regions are `[JsonIgnore]` — never persisted, always computed. `ComputeDerivedAreas()` is called at three points: (1) `ServiceExtensions.AddBaseConfig()` after JSON load, (2) `ScreenSplitterOverlayControl.WriteToOptions()` after UI save, (3) `CopyFrom()` after reload. Regions default to all-zero coordinates (meaning "unconfigured") and are skipped by the router.

`YoloOptions.ClassNames` defaults to `[]` (empty). When loading from JSON, MS Config binds by appending to existing values — having an empty default prevents duplicate entries. If the JSON file is missing, class names must be provided by the YOLO model metadata instead.

## Service Lifetimes

- **Singleton**: `GameStateTracker`, `GameRecorderService` (must persist state across frames), `FrameStateHub` (shared across scopes), `ScreenCaptureOptions` (config), `PipelineOptions` (config), `OverlayOptions` (config)
- **Scoped**: `YoloDetectorPoolService`, `IScreenCaptureService`, `FrameFusionService`, `FrameAnalyzerService`, `TileAnalysisService`, `GamePipelineService`

`GamePipelineService` is scoped because it depends on scoped Vision services. It delegates state to the singleton `GameRecorderService`. `FrameStateHub` must be singleton because the pipeline (scoped) and UI ViewModels (long-lived) must share the same event hub instance.

## PipelineOptions

`PipelineOptions` in `TileMind.Common.Config/` controls pipeline behavior (currently not persisted to JSON):

- `EnableStateTracking = true` — enables `GameStateTracker` for cross-frame action classification. When `false`, the pipeline stops after static analysis and only publishes `AnalyzedFrame` to UI.

## Dora Indicator Mapping

`FrameAnalyzerService.GetDoraTileType()` maps indicator tile → actual dora per standard Japanese mahjong rules:
- Number tiles: 1→2, 2→3, …, 8→9, 9→1
- Winds: East→South→West→North→East
- Dragons: White→Green→Red→White
- Red fives (0) are normalized to 5 before mapping

Output stored in `AnalyzedFrame.DoraTiles` (deduplicated `List<TileType>`).

## Tile Analysis Integration

`TileAnalysisService` in `TileMind.Algorithm` bridges `AnalyzedFrame` to [RiichiSharp](https://github.com/zzijin/RiichiSharp) (self-developed .NET mahjong algorithm library, available as NuGet package at `E:\NuGetPackages\MahjongAlgorithms.1.0.0`).

### Tile Type Mapping

`TileTypeMapper` converts between TileMind's `TileType` enum (`suit*10+offset`: M1=0..M9=8, M0=9, P1=10..Z7=36) and RiichiSharp's `Tile` struct (`suit*9+offset`: m1=0..m9=8, p1=9..s1=18..honors 27-33). Red fives (M0/P0/S0: offset=9 within suit) are mapped to normal M5/P5/S5 (offset=4); the aka-dora count is tracked separately via `GameContext.AkaCount`.

### RiichiSharp APIs Used

| API | When | Output |
|-----|------|--------|
| `MahjongEngine.Shanten(handStr, calledMelds)` | Every frame | Shanten number (-1=complete, 0=tenpai, 1+=distance) |
| `TenpaiCalculator.Calculate(tiles, calledMelds, visible)` | Shanten==0 | Wait tiles with remaining counts |
| `MahjongEngine.Score(handStr, context)` | Per wait tile | Yaku, han, fu, points |
| `MahjongEngine.EffectiveDiscards(handStr, calledMelds)` | Shanten>0 | Per-tile discard analysis with ukeire |

### Edge Cases

- Empty hand → returns Shanten=6 immediately
- Shanten==-1 (completed hand) → skips both win options and discard analysis; UI displays "Agari!"
- Score failure per wait → records wait tile with remaining count, no score info
- Round/seat winds default to East (field info not yet parsed from InfoArea); <1000-2000 point error

## Overlay System

The WPF overlay draws on a full-screen transparent window (`OverlayWindow` → `OverlayWindowViewModel` → `MahjongOverlayControl`). A separate non-transparent toolbar window (`OverlayToolbarWindow`) provides controls (start/stop/close). The overlay window uses `WS_EX_LAYERED | WS_EX_TRANSPARENT` for full mouse click-through.

**Feature toggles** (`OverlayOptions` in `TileMind.Common.Config/`, registered as Singleton):
- `ShowDetectionBoxes` — bounding boxes + tile name labels for all detected tiles
- `ShowScreenRegions` — semi-transparent polygons marking configured `ScreenCaptureOptions` regions (9 zones)
- `ShowTimingStats` — per-step timing and FPS display (top-left)
- `ShowRemainingTiles`, `ShowWinningAnalysis`, `ShowActionLog`, `ShowAIDecision` — planned

**Coordinate transform**: `OverlayWindow.OnSourceInitialized()` sets `TransformFromDevice` matrix on `OverlayBaseControl` to convert screen physical pixels → WPF DIPs (supports high-DPI). The transform is always applied in `RenderDrawingInfo()`.

**Drawing pipeline**: `DrawingInfo` objects carry `List<IDrawingCommand>`. Commands: `RectangleCommand`, `TextCommand`, `PolygonCommand`, `EllipseCommand`. `OverlayBaseControl` manages `DrawingVisual` objects via `VisualCollection`, exposes them through `GetVisualChild`/`VisualChildrenCount` overrides, and handles mouse hit-test pass-through via `HitTestCore`. Styles are provided by `MahjongOverlayControl.GetDrawingStyles()` which color-codes by seat and region type.

**Event hub**: `FrameStateHub` is a **Singleton** bridging Core → UI:
- `FrameAnalyzed(AnalyzedFrame)` — stage 1 (every frame)
- `ActionsDetected(List<MahjongAction>)` — stage 2 (tracking mode only)
- `FrameTiming(FrameTimingInfo)` — per-step timing (every frame)
- `TileAnalysisReady(TileAnalysisResult)` — shanten/tenpai/scoring analysis (every frame)

**Data flow**: `OverlayWindowViewModel` subscribes to `FrameAnalyzed` and `FrameTiming`, updates `OverlayItems` (bound to `MahjongOverlayControl.ItemsSource` in code-behind). Screen regions are static and drawn once on construction; detection boxes and FPS text are replaced each frame.

## Static vs Stateful Analysis

| Layer | Service | Input → Output | History? |
|-------|---------|---------------|----------|
| Static | `FrameAnalyzerService` | `FrameDetections` → `AnalyzedFrame` | No |
| Static | `TileAnalysisService` | `AnalyzedFrame` → `TileAnalysisResult` | No |
| Stateful | `GameStateTracker` | `AnalyzedFrame` → `List<MahjongAction>` | Yes |

`FrameAnalyzerService` handles: `HandMeldSeparator` (hand/meld separation, per-seat logic), `DetermineMeldType` (Chi/Pon/Kan), `InferAnkans` (2 same-tiles + side gaps → 4-tile Ankan), dora indicator mapping, riichi detection via aspect ratio. These are single-frame operations needed in both tracking and non-tracking modes.

`TileAnalysisService` handles: `TileTypeMapper` (TileMind ↔ RiichiSharp tile mapping), `HandStringBuilder` (AnalyzedFrame → tenhou-format hand strings), calls RiichiSharp for shanten/tenpai/scoring via `Shanten`, `EffectiveDiscards`, `TenpaiCalculator`, `Score`. Outputs `TileAnalysisResult` (shanten number, remaining counts, discard options with ukeire, win options with yaku/points).

`GameStateTracker` handles: `TileTracker` (IoU matching), `DetectNewMelds` (is this meld new or existing), `DetectKakan` (Pon→Kakan upgrade), `ActionClassifier` (hand/pond diffs → actions), error frame detection (pond drop ≥2). These require cross-frame state.

### Active Player Determination (removed from static analysis)

Active player logic is not yet implemented. It will be added to `GameStateTracker` for multi-action frame decomposition (5c). The formula is:
```
total = HandTiles.Count + Σ(Melds[i].Tiles.Count)
kanCount = Melds.Count(m => m.MeldType is Kan/Ankan/Kakan)
Active: total == 14 + kanCount
```
If no player matches exactly, the one with highest `total` is chosen (fallback). If still ambiguous, `ActivePlayer` is `null` — the pipeline skips state tracking for this frame.

### Riichi Detection

`FrameAnalyzerService.DetectRiichi()` checks discard pond detection bounding-box aspect ratio per seat orientation:

| Seat | Normal tile | Riichi (rotated 90°) |
|------|------------|----------------------|
| Self / Opposite | h > w | w > h |
| Right / Left | w > h | h > w |

Result stored in `PlayerFrameAnalysis.HasRiichiDiscard` / `RiichiDiscardTile`.

### Error Frame Handling

**Tracking layer**: When a player's discard pond count drops by 2 or more (impossible in real mahjong), the frame is treated as erroneous and skipped for that player (logs Warning).

### Action Classification

`ActionClassifier.ClassifyPlayerActions()` maps `PlayerFrameDelta` → `List<MahjongAction>`. Current rules:

| handDiff | pondDiff | meldsAdded | Result |
|----------|----------|------------|--------|
| +1 | 0 | 0 | Draw |
| -1 | +1 | 0 | Discard |
| 0 | +1 | 0 | Discard (same-frame Draw+Discard) |
| — | 0 | 1 (3 tiles) | Chi or Pon |
| — | 0 | 1 (4 tiles) | Kan (handDiff -3) or Ankan (handDiff -4) |
| — | — | upgradedMeld | Kakan |

Unmatched deltas are logged via `Debug.WriteLine` with frame number, seat, hand/pond/meld change counts for later analysis.

### Meld Source Tracking

Each `TrackedTile` has `SourcePlayer` (`null` = drawn from wall, else = whose discard was taken). Set by `ActionClassifier.EnrichMeldSources()` when the source player's discard tile type matches a tile in the meld. Only type-level matching is implemented; exact tile (position-based) identification is planned.

## Planned (Not Yet Implemented)

### Multi-Action Frame Analysis (5b, 5c)

When two frames skip intermediate actions (e.g., Player A discards → Player B immediately calls Chi, all within one frame gap), the current single-delta classifier cannot decompose the composite change. Planned approach:

1. **5b — Simple multi-action**: Detect patterns like `handDiff == -2, pondAdded == 1` → split into 2× Discard.
2. **5c — Rule-based decomposition**: Use `ActivePlayer` sequence + mahjong rules to systematically decompose multi-step frame deltas:
   - Determine active player from AnalyzedFrame
   - Use hand/meld/pond change rules to infer operation count
   - Kan operations imply extra draws
   - Cross-player action linking (whose discard was taken)

### Riichi Action Generation

`PlayerFrameAnalysis.HasRiichiDiscard` is computed but not yet consumed by `GameStateTracker` to set `PlayerState.IsRiichi` and generate `ActionType.Riichi`.

### Meld Trigger Tile Position

Chi/Pon trigger tile (rotated within meld group) position detection — left/center/right for Chi, which-side for Pon — to precisely identify source player without relying on type-level discarding matching alone.

## Meld Type Detection

### HandMeldSeparator

`HandMeldSeparator.Separate(detections, seat)` processes `HandAndMeldArea` detections per seat:

| Seat | HandAndMeldArea contains | Separation logic |
|------|------------------------|------------------|
| **Self** | Hand tiles (face-up, visible) + meld tiles (face-up) | Sort by primary axis → find max gap → larger group = hand, smaller = meld |
| **Right/Opposite/Left** | Meld tiles only (hand tiles are face-down, not detected by YOLO) | No separation; all detections treated as meld candidates |

**Algorithm** (Self only):
1. Sort all detections along primary axis (X for horizontal seats, Y for vertical)
2. Compute gaps between consecutive tiles
3. If max gap > avg gap × `HandMeldGapMultiplier` (2.0) → split point found
4. Larger side = hand (13-14 tiles), smaller side = meld candidates
5. Meld candidates clustered by proximity (≤10px) into groups of 2-4 tiles

**Meld direction per seat**:
- Self: hand left, meld right (X axis, meldsAfterHand=true)
- Opposite: hand right, meld left (X axis, meldsAfterHand=false)
- Left (上家): hand top, meld bottom (Y axis, meldsAfterHand=true)
- Right (下家): hand bottom, meld top (Y axis, meldsAfterHand=false)

### DetermineMeldType

Chi vs Pon vs Kan from tile count and type patterns:
- 4 tiles → Kan
- 3 tiles + same-suit sequential → Chi, otherwise → Pon
- Red fives (M0/P0/S0) normalized to M5/P5/S5 for sequence comparison

### InferAnkans

After separation, 2-tile same-type groups with large gaps on both sides (>1.5 tile width) are inferred as Ankan. The 2 detected middle face-up tiles are duplicated to fill the 4-tile group, enabling `DetermineMeldType` to correctly identify count=4 → Kan → ActionClassifier sees handDiff=-4 → Ankan.
