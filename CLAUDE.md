# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.
- Exception: Do not sacrifice established patterns that improve long-term maintainability or testability just for immediate simplicity.
- Exception: Never compromise security for simplicity. Security is a high priority and justifies necessary complexity within healthy limits.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, but if you notice bad habits or poor practices, point them out and ask before continuing them.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" â†’ "Write tests for invalid inputs, then make them pass"
- "Fix the bug" â†’ "Write a test that reproduces it, then make it pass"
- "Refactor X" â†’ "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] â†’ verify: [check]
2. [Step] â†’ verify: [check]
3. [Step] â†’ verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

## 5. Signal Uncertainty

**Don't state guesses as facts. When confidence is low, say so.**

When your knowledge is incomplete, a hallucination inferred, or unverified:
- Preface responses with "possibly", "likely", "I'm not certain", or "you should verify this" â€” don't omit them.
- Distinguish between what you know and what you're inferring.
- If a claim requires external verification before acting on it, flag that explicitly.
- Never let confident tone substitute for confident knowledge.

When you notice you're filling a gap with an assumption:
- Name the gap: "I don't have visibility into X, so I'm assuming Y."
- Offer to stop rather than guess: "I can proceed on that assumption, or you can verify first."
- Don't bury uncertainty at the end of a long confident response.

The test: Could a developer act on this response and only discover it was wrong after the damage is done? If yes, the uncertainty wasn't signalled clearly enough.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

## What this is

Region to Share is a small Windows WPF desktop app that mirrors a chosen screen region into a window, so users can share that window through meeting apps (Teams, WebEx, etc.) that only allow sharing a full screen or a single window. The app itself is unaware of the meeting app — it just captures pixels and presents them.

## Build / run

Solution is `src/RegionToShareEx.slnx` (modern XML solution format). The app targets **.NET 10** (`net10.0-windows10.0.22621.0`, WPF, `WinExe`) and builds with the .NET SDK:

```cmd
# Restore + build (Release)
dotnet build src/RegionToShareEx.slnx -c Release

# Run after building
src/RegionToShareEx/bin/Release/net10.0-windows10.0.22621.0/RegionToShareEx.exe

# Or build + run in one step
dotnet run --project src/RegionToShareEx/RegionToShareEx.csproj

# Clean obj/bin and stray *_wpftmp.csproj
src/clean.cmd
```

`SupportedOSPlatformVersion` is 10.0.19041.0 (Windows 10 2004) — the minimum for Windows.Graphics.Capture. There are **no unit tests**.

Version lives in `src/Directory.Build.props` (`<Version>`). CI is GitHub Actions (`.github/workflows/build.yml`): builds the app on `windows-latest` and uploads the publish output as an artifact.

## Project layout

A single project: `src/RegionToShareEx/RegionToShareEx.csproj` (assembly/exe `RegionToShareEx`, namespace `RegionToShareEx`). The only NuGet dependency is **Vortice.Direct3D11** (D3D11 interop for the capture path). `src/Assets` holds the source icons/GIFs referenced by the README.

> This is a fork of tom-englert/RegionToShare (**GPLv3** — keep the license and original attribution). The original Microsoft Store packaging (`Packaging.wapproj`), the `ImagePadding` asset tool, and the `TomsToolbox`/`Fody` dependencies were removed in the v2 rewrite.

## Architecture

The whole app is two windows plus a thin native-interop layer.

- **MainWindow** (`MainWindow.xaml.cs`) — the resizable frame the user positions over the region to share. It owns the Win32 window handle and a hidden **separation layer** window (a borderless window at the bottom of the z-order). When the main window is sent to the back (`SendToBack`), it sits just above the separation layer, so the meeting app keeps capturing the "Region to Share Ex" window while the actual desktop region underneath shows through to the user. `BringToFront`/`SendToBack` toggle this.

- **RecordingWindow** (`RecordingWindow.xaml.cs`) — created when the user clicks the main window to start capturing. It drives the **`ScreenCapture`** engine and presents frames into the main window's `RenderTarget` image via a `WriteableBitmap` (updates coalesced with a `DispatcherThrottle`). Custom `WM_NCHITTEST` handling (`NcHitTest`) lets the frame border be dragged/resized while the interior stays click-through-transparent.

- **ScreenCapture** (`ScreenCapture.cs`) + **Direct3D11Interop** (`Direct3D11Interop.cs`) — the GPU capture path. Uses **Windows.Graphics.Capture** to capture the monitor containing the region, GPU-crops the region into a staging texture (Vortice/D3D11), and reads it back to a CPU buffer the RecordingWindow presents. The cursor toggle maps to `GraphicsCaptureSession.IsCursorCaptureEnabled`; the yellow capture border is hidden via `IsBorderRequired = false` (Windows 11 only). **Limitation:** WGC captures one monitor, so a region spanning two monitors shows black outside the primary monitor.

- **NativeMethods** / **ExtensionMethods** / **WpfExtensions** — P/Invoke wrappers (`SetWindowPos`, `GetWindowRect`, `DwmGetExtendedFrameBounds`, `MonitorFromRect`/`GetMonitorInfo`) and small self-contained WPF helpers (`DispatcherThrottle`, `BeginInvoke`, `GetWindowHandle`, `AncestorsAndSelf`, `ExceptionChain`) that replaced the former TomsToolbox usage. Coordinate math works in **native device pixels**, converting to/from WPF units via `HwndTarget` composition-target transforms.

Key coordination details:
- `GlassFrameThickness` (DWM extended frame bounds) is added/subtracted everywhere to reconcile WPF's logical window rect with the real native rect; get this wrong and the captured region drifts from the visible frame.
- Position/size updates are funneled through `UpdateSizeAndPos`, throttled via `DispatcherThrottle` to coalesce rapid move/resize events.
- A "debug offset" (Ctrl+Alt+Shift on click) shifts the capture source so you can develop the app and watch the captured output side-by-side on the same machine.

## Settings & persistence

User settings use `ApplicationSettingsBase` (`Properties/Settings.cs`, hand-written): theme color, FPS, cursor capture, start-activated, and serialized window placement. `MainWindow.ValidateSettings` self-heals a corrupt settings file and clamps FPS to `SupportedFramesPerSecond`. A user-editable `resolutions.txt` under `%AppData%/RegionToShareEx` provides the resolution presets dropdown.

## Conventions

- Nullable reference types and `ImplicitUsings` are enabled; `LangVersion` latest.
- UI styling uses WPF's built-in **Fluent dark theme** (`ThemeMode="Dark"` in `App.xaml`; the experimental `WPF0001` diagnostic is suppressed in the csproj). The `ThemeColor` app resource (default SteelBlue) still drives the recording frame's accent.
