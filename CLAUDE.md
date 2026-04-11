# Kerpilot - Claude Code Guidelines

## Project Overview

KSP (Kerbal Space Program 1.12.5) mod providing an in-game AI chat assistant using Unity uGUI, connected to any OpenAI-compatible LLM API.

## Build

```bash
dotnet build -c Release
```

Output: `GameData/Kerpilot/Plugins/Kerpilot.dll`

KSP managed DLLs referenced from:
`/Users/linear/Library/Application Support/Steam/steamapps/common/Kerbal Space Program/KSP.app/Contents/Resources/Data/Managed/`

## Architecture

```
Kerpilot.csproj              # net472, references KSP/Unity DLLs from game directory
src/
  KerpilotAddon.cs           # Entry point ([KSPAddon]), toolbar button, Ctrl+K shortcut
  ChatWindow.cs              # Builds full uGUI hierarchy programmatically (Canvas, ScrollRect, InputField)
  ChatMessage.cs             # Data model: MessageSender enum + ChatMessage class
  ChatBubbleFactory.cs       # Creates rounded-rect sprites and message bubble GameObjects
  UIStyleConstants.cs        # Static design tokens: colors, dimensions, font sizes
  KerpilotSettings.cs        # Settings persistence via KSP ConfigNode (API key, endpoint, model)
  SettingsPanel.cs           # uGUI settings form (same-window panel swap with chat view)
  LlmClient.cs               # LLM API client using UnityWebRequest with SSE streaming
  JsonHelper.cs              # Minimal JSON utilities (escape, extract, build request body)
GameData/Kerpilot/
  Plugins/                   # Deployed DLL (symlinked into KSP GameData)
  PluginData/settings.cfg    # User settings (created at runtime, not committed)
```

Key design decisions:
- All UI is built programmatically via uGUI (no asset bundles, no OnGUI/IMGUI)
- Rounded-rect sprites generated at runtime with 9-slice for bubble backgrounds
- **LLM streaming**: Uses `UnityWebRequest` with a custom `DownloadHandlerScript` subclass (`SseDownloadHandler`) to parse SSE chunks. UI updates are throttled to ~10fps via a dedicated `StreamingUiLoop` coroutine to avoid layout rebuild spam. `ChatMessage` stays immutable — only the UI `Text` component is updated during streaming; the final `ChatMessage` is created on completion.
- **Settings persistence**: Uses KSP `ConfigNode` system, saved to `GameData/Kerpilot/PluginData/settings.cfg`. Settings panel swaps in-place with the chat view (same window, no second window).
- **Input lock**: `InputLockManager.SetControlLock(ControlTypes.All)` via `EventTrigger` callbacks on the InputField (`Select` → lock, `Deselect` → unlock). Must use event callbacks, not per-frame `isFocused` polling (polling has frame-ordering issues causing keystroke leakage). `ControlTypes.All` blocks all controls including camera while typing.
- **UI rendering sharpness requirements:**
  - Font: use `UISkinManager.defaultSkin.font` (KSP native font), never `Resources.GetBuiltinResource<Font>("Arial.ttf")` (low-quality bitmap). TextMeshPro is unavailable (KSP 1.12.5 does not expose TMPro assemblies).
  - Canvas: set `pixelPerfect = true` to force pixel-aligned rendering
  - CanvasScaler: use `ConstantPixelSize` with `scaleFactor = 1` (no canvas scaling). All sizes are manually computed via `UIStyleConstants.Scaled()`/`ScaledFont()` based on `Screen.height / 1080f`. This ensures font sizes are rounded to integers so the font atlas renders at exact pixel boundaries — CanvasScaler scaling renders at base size then upscales, causing blur.
  - Font sizes: minimum 12px for dynamic fonts (below 12 renders poorly in Unity)

## Rules

1. **Rebuild after code changes**: After adding or modifying code, always run `dotnet build -c Release` and verify 0 errors before proceeding.
2. **Update README.md on new features**: When adding new user-facing functionality, update README.md to reflect the changes.
3. **Update CLAUDE.md on architecture changes**: When the project structure, build system, or class responsibilities change, update this file to stay accurate.
4. **Run /simplify before commit**: Before committing, run `/simplify` to review changed code for reuse, quality, and efficiency.
