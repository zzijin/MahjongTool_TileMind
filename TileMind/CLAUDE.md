# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build                    # Build entire solution
dotnet run --project TileMind.Console   # Console test harness
dotnet run --project TileMind.UI        # WPF desktop app
```

No test project exists yet. The solution uses .NET 10, Windows-only (WPF + DXGI).

## Architecture

TileMind is a real-time mahjong AI assistant. Six projects in a pipeline:

```
TileMind.Vision → TileMind.Core → TileMind.UI
       ↓                ↓
  TileMind.Common   TileMind.AI (placeholder)
```

- **TileMind.Common** — Shared types (`TileType` enum, `DetectionResult`, config options, game state models). Everything else depends on this.
- **TileMind.Vision** — Screen capture (DXGI Desktop Duplication via SharpDX), YOLOv8 ONNX inference (GPU via CUDA, CPU fallback), multi-frame fusion.
- **TileMind.Core** — DI wiring (`ServiceExtensions`), game state tracking pipeline (`GamePipelineService` → `GameRecorderService` → `GameStateTracker`), hand/meld separation, IoU-based frame-to-frame tile matching, action classification.
- **TileMind.UI** — WPF desktop app using WPF-UI library (Fluent Design). Transparent overlay window for drawing detection results, navigation pages (Home, Settings), tray icon.
- **TileMind.AI** — Empty project, reserved for future AI decision logic.

## Key Pipeline

`GamePipelineService.ProcessFrame()` is the main entry point:

1. `FrameFusionService.ProcessFrameFusion()` — captures N frames, runs YOLO on each, fuses by weighted voting
2. `RouteDetections()` — assigns each detection to a player/region by testing whether its center falls inside the quadrilateral regions defined in `ScreenCaptureOptions` (point-in-convex-quad test with cross products)
3. `GameRecorderService.ProcessFrame(FrameDetections)` — separates hand/meld via gap analysis, matches tiles across frames by IoU, computes per-player state diffs, classifies actions (Draw/Discard/Chi/Pon/Kan/Ankan)

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

`ScreenCaptureOptions` defines 9 quadrilateral regions (`Point[4]` each) that partition the screen. These are used by `GamePipelineService` to route detections. Regions default to all-zero coordinates (meaning "unconfigured") and are skipped by the router.

## Service Lifetimes

- **Singleton**: `GameStateTracker`, `GameRecorderService` (must persist state across frames)
- **Scoped**: `YoloDetectorPoolService`, `IScreenCaptureService`, `FrameFusionService`, `GamePipelineService`

`GamePipelineService` is scoped because it depends on scoped Vision services. It delegates state to the singleton `GameRecorderService`.

## Meld Type Detection

When only a combined hand+meld area is available (as is the case for all four players), `HandMeldSeparator` separates them by gap analysis: cluster detections along the hand axis, find the largest gap between consecutive tiles, treat the tight cluster as meld. Chi vs Pon is distinguished by tile type patterns (same-suit sequential = Chi, same tile type = Pon). Red fives (M0/P0/S0) are normalized to M5/P5/S5 for sequence comparison.
