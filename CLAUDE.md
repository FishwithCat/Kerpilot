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
  ChatMessage.cs             # Data model: MessageSender/MessageRole enums, ToolCall, ChatMessage
  KerpilotSettings.cs        # Settings persistence via KSP ConfigNode (API key, endpoint, model)
  UI/                        # User interface
    ChatWindow.cs            # Core window lifecycle, state, utilities (partial class)
    ChatWindow.UI.cs         # UI construction: Canvas, header, message area, inline input (partial)
    ChatWindow.Input.cs      # Input handling: text changes, send (partial)
    ChatWindow.Streaming.cs  # LLM streaming: tool-call loop, UI updates during streaming (partial)
    ChatBubbleFactory.cs     # Sprite generation (rounded-rect, gear) and terminal-style message lines
    UIStyleConstants.cs      # Static design tokens: terminal colors, dimensions, font sizes
    SettingsPanel.cs         # uGUI settings form (same-window panel swap with chat view)
    DragHandler.cs           # MonoBehaviour for window dragging via header
  Api/                       # LLM communication
    LlmClient.cs             # API client using UnityWebRequest with SSE streaming + tool call parsing
    JsonHelper.cs            # JSON utilities (escape, extract, build request body, tool call parsing)
  Tools/                     # KSP game data and domain knowledge
    ToolDefinitions.cs       # Tool JSON schemas, dispatch to GameDataTools
    GameDataTools.cs         # KSP game data queries, vessel capability analysis (vessel parts, celestial bodies, contracts, Δv analysis, etc.)
    SkillDefinitions.cs      # Domain knowledge skills: orbital mechanics, rocket design, delta-v budget
    SkillSelector.cs         # Keyword-based skill matching, dynamic system prompt composition
GameData/Kerpilot/
  Plugins/                   # Deployed DLL (symlinked into KSP GameData)
  PluginData/settings.cfg    # User settings (created at runtime, not committed)
tests/
  Kerpilot.Tests.csproj      # NUnit test project (net472, references main project + KSP DLLs)
  ToolAvailabilityTests.cs   # Tests for tool definitions, dispatch, JSON parsing, request body
  SkillTests.cs              # Tests for skill definitions, keyword selection, prompt composition
```

Key design decisions:
- All UI is built programmatically via uGUI (no asset bundles, no OnGUI/IMGUI)
- **Terminal-style interface**: Messages displayed as plain text lines (no bubble backgrounds). User messages prefixed with green `> `, AI responses in bright white. Input is inline at the bottom of the scrolling content area (no separate input bar) — like typing in a real console. `ChatBubbleFactory.CreateTerminalRow` provides the shared row layout for both message lines and the input row.
- Rounded-rect sprites generated at runtime with 9-slice (used by settings panel input fields)
- **LLM streaming**: Uses `UnityWebRequest` with a custom `DownloadHandlerScript` subclass (`SseDownloadHandler`) to parse SSE chunks and accumulate tool call fragments. UI updates are throttled to ~10fps via a dedicated `StreamingUiLoop` coroutine to avoid layout rebuild spam. `ChatMessage` stays immutable — only the UI `Text` component is updated during streaming; the final `ChatMessage` is created on completion.
- **Tool calling (function calling)**: Supports OpenAI-compatible tool use. `ToolDefinitions` provides 10 tool JSON schemas and dispatches to `GameDataTools` which queries KSP APIs (`FlightGlobals`, `PartLoader`, `CelestialBody`, `ContractSystem`). `ChatWindow.StreamLlmResponse` runs a multi-round coroutine loop (max 5): if the LLM responds with `tool_calls`, tools are executed synchronously and results sent back until the LLM produces a text response. All tools require flight scene. Per-tool status labels (e.g. "Calculating delta-v...") are shown as plain italic text during execution.
- **Skills (domain knowledge injection)**: `SkillDefinitions` stores 3 knowledge skills (orbital mechanics, rocket design, delta-v budget) as C# string constants. `SkillSelector` uses keyword matching on the user's latest message to select up to 2 relevant skills and appends their content to the system prompt. This happens in `LlmClient.SendChatRequest` before building the request body. Skills are compiled into the DLL (no external files).
- **Settings persistence**: Uses KSP `ConfigNode` system, saved to `GameData/Kerpilot/PluginData/settings.cfg`. Settings panel swaps in-place with the chat view (same window, no second window).
- **Input lock**: `InputLockManager.SetControlLock(ControlTypes.All)` via `EventTrigger` callbacks on the InputField (`Select` → lock, `Deselect` → unlock). Must use event callbacks, not per-frame `isFocused` polling (polling has frame-ordering issues causing keystroke leakage). `ControlTypes.All` blocks all controls including camera while typing.
- **UI rendering sharpness requirements:**
  - Font: use `UISkinManager.defaultSkin.font` (KSP native font), never `Resources.GetBuiltinResource<Font>("Arial.ttf")` (low-quality bitmap). TextMeshPro is unavailable (KSP 1.12.5 does not expose TMPro assemblies).
  - Canvas: set `pixelPerfect = true` to force pixel-aligned rendering
  - CanvasScaler: use `ConstantPixelSize` with `scaleFactor = 1` (no canvas scaling). All sizes are manually computed via `UIStyleConstants.Scaled()`/`ScaledFont()` based on `Screen.height / 1080f`. This ensures font sizes are rounded to integers so the font atlas renders at exact pixel boundaries — CanvasScaler scaling renders at base size then upscales, causing blur.
  - Font sizes: minimum 12px for dynamic fonts (below 12 renders poorly in Unity)

## Tests

```bash
dotnet test tests/Kerpilot.Tests.csproj -c Release
```

NUnit test suite (`tests/ToolAvailabilityTests.cs`) verifies tool infrastructure without requiring a running KSP instance:
- **Tool definitions**: All 10 tools present in JSON array, each with description and parameters schema, required parameters correct
- **Status labels**: Every tool name maps to a non-empty label ending in "..."
- **ExecuteTool dispatch**: Unknown tools return error JSON with escaped names; missing required params return errors (not exceptions)
- **JsonHelper parsing**: `ExtractJsonStringValue` for tool arguments, SSE `tool_calls` detection/extraction (index, id, function name, arguments fragments)
- **ChatMessage model**: `ToolCall` properties, `CreateAssistantToolCall`/`CreateToolResult` factory methods
- **Request body**: `BuildChatRequestBody` includes tools JSON, serializes tool call history correctly, omits tools field when null

NUnit test suite (`tests/SkillTests.cs`) verifies the skill system:
- **Skill definitions**: All 3 skills present with non-empty Id, Title, Content, Keywords
- **Skill selection**: Keyword matching returns correct skills for domain queries, empty for unrelated input, max 2 results
- **Prompt composition**: Base prompt unchanged when no skills match, skill content appended correctly when matched

## Rules

1. **Rebuild after code changes**: After adding or modifying code, always run `dotnet build -c Release` and verify 0 errors before proceeding.
2. **Update README.md on new features**: When adding new user-facing functionality, update README.md to reflect the changes.
3. **Update CLAUDE.md on architecture changes**: When the project structure, build system, or class responsibilities change, update this file to stay accurate.
4. **Run /simplify before commit**: Before committing, run `/simplify` to review changed code for reuse, quality, and efficiency.
