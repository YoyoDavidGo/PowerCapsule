# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build

```
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" "E:\AI\software\PowerCapsule\PowerCapsule\PowerCapsule.csproj" /t:Build /p:Configuration=Debug
```

- .NET Framework 4.8 WPF (WinExe), not .NET Core/5+. `dotnet build` does NOT compile XAML — always use MSBuild.
- Output: `PowerCapsule\bin\Debug\PowerCapsule.exe`
- Single dependency: Newtonsoft.Json 13.0.3 (packages dir)

## Architecture

MVVM + Services, no DI container. Services are created in `CapsuleWindow` constructor and passed to ViewModels manually.

```
App.xaml.cs → CapsuleWindow (main floating widget, 270×42px pill)
                ├── CapsuleViewModel (1s timer → live status text)
                ├── Popup → DropPanel (3-tab settings: 定时关机/防止睡眠/定时唤醒)
                │            ├── ShutdownViewModel
                │            ├── SleepPreventViewModel
                │            └── WakeViewModel
                ├── TrayService (system tray icon + context menu)
                └── SettingsView (separate window, opened from tray)
```

**Services** — each wraps a Windows OS feature via P/Invoke or process calls:
- `ShutdownService` — `shutdown /s /t N` / `shutdown /a`
- `SleepPreventService` — `SetThreadExecutionState` (ES_SYSTEM_REQUIRED / ES_DISPLAY_REQUIRED / ES_CONTINUOUS)
- `WakeTaskService` — `schtasks` for wake-from-sleep timers
- `ConfigService` — JSON persistence via Newtonsoft to user-local path
- `StartupService` — registry Run key
- `TrayService` — `System.Windows.Forms.NotifyIcon`

**Capsule display priority** (in `CapsuleViewModel.GetCurrentStatus()`): Critical shutdown countdown (≤60s) > timed shutdown > timed wake > prevent sleep > idle.

## Key WPF gotchas

### Popup input responsiveness
The outer `DropPanelPopup` MUST keep `AllowsTransparency="True"`. Layered windows route clicks directly to controls without requiring activation first. Changing it to `False` makes every first click consumed by window activation — controls need a second click to respond.

The inner ComboBox dropdown Popup must use `AllowsTransparency="False"` to avoid nested-layered-window focus conflicts that prevent the dropdown from opening.

The main `CapsuleWindow` also uses `AllowsTransparency="True"` + `WindowStyle="None"` for the transparent capsule shape. A `WM_MOUSEACTIVATE` WndProc hook forces `MA_ACTIVATE` to ensure drag and click responsiveness.

### Capsule edge-snap state machine
The capsule has 4 states managed by bools `_isEdgeSnapped`, `_isHidden`, `_isLeftEdge`:
- `SnapToEdge` → shows EdgeBar overlay, moves window so only 5px visible
- `SlideOut` → hides EdgeBar, animates capsule to full visibility
- `SlideIn` → shows EdgeBar, animates capsule to edge
- `CancelSlideAnimation` → kills animation + jumps to expanded position (called from any MouseLeftButtonDown before drag)

**Critical:** `CapsuleBorder` must NEVER be `Visibility.Collapsed`. If collapsed, mouse events can't reach it after expanding — drag breaks. The EdgeBar is a z-order overlay on top of an always-visible CapsuleBorder.

Animation race condition: `_slideGeneration` counter prevents stale `Completed` handlers from corrupting visibility state when SlideIn/SlideOut overlap.

### Focus inside Popup
When the panel opens, `DropPanelPopup_Opened` adds a `WndProc` hook to the Popup's internal `HwndSource` and calls `DropPanelControl.Focus()` + `Keyboard.Focus()` via `Dispatcher.BeginInvoke(DispatcherPriority.Loaded)` — the Popup window handle isn't ready until then.
