# Kerpilot - Claude Code Guidelines

## Project Overview

KSP (Kerbal Space Program 1.12.5) mod providing an in-game AI chat assistant using Unity uGUI, connected to any OpenAI-, Anthropic-, or Google-Gemini-compatible LLM API. The provider protocol is auto-detected from the configured Base URL: URLs containing `generativelanguage.googleapis.com` (without `/openai` in the path) or ending in `/gemini` use the native Gemini `:streamGenerateContent` API; URLs containing `api.anthropic.com` or ending in `/anthropic` use the Anthropic Messages API; everything else uses OpenAI Chat Completions.

## Build

```bash
dotnet build -c Release
```

Output: `GameData/Kerpilot/Plugins/Kerpilot.dll`

KSP path is resolved via: `KSPROOT` env var > MSBuild `-p:KSPRoot=...` > default Steam path on macOS.

Package for distribution: `dotnet msbuild Kerpilot.csproj -t:Package -p:Configuration=Release` → `dist/Kerpilot-vX.Y.Z.zip`

KSP managed DLLs referenced from:
`$(KSPRoot)/KSP.app/Contents/Resources/Data/Managed/`

## Architecture

```
Kerpilot.csproj              # net472, references KSP/Unity DLLs from game directory
src/
  KerpilotAddon.cs           # Entry point ([KSPAddon]), toolbar button, Ctrl+K shortcut
  ChatMessage.cs             # Data model: MessageSender/MessageRole enums, ToolCall, ChatMessage
  KerpilotSettings.cs        # Settings persistence via KSP ConfigNode (API key, endpoint, model)
  UI/                        # User interface
    ChatWindow.cs            # Core window lifecycle, state, rich-text log management (partial class)
    ChatWindow.UI.cs         # UI construction: Canvas, header, message area, inline input (partial)
    ChatWindow.Input.cs      # Input handling: text changes, send (partial)
    ChatWindow.Streaming.cs  # LLM streaming: tool-call loop, UI updates during streaming (partial)
    SpriteFactory.cs         # Runtime sprite generation (rounded-rect for settings, gear icon)
    UIStyleConstants.cs      # Static design tokens: terminal colors (Color + hex), dimensions, font sizes
    SettingsPanel.cs         # uGUI settings form (same-window panel swap with chat view)
    DragHandler.cs           # MonoBehaviour for window dragging via header
  Api/                       # LLM communication
    ChatProvider.cs          # ChatProvider enum (OpenAICompatible/Anthropic/Gemini) + ChatProviderDetector (auto-detect from Base URL)
    LlmClient.cs             # API client using UnityWebRequest with SSE streaming + tool call parsing; branches on provider for URL/headers/body, shared SseDownloadHandler takes a multi-delta parser delegate
    JsonHelper.cs            # Manual JSON utilities: escape, extract, build OpenAI/Anthropic/Gemini request bodies, brace-balanced scope-aware SSE delta parsers (OpenAI + Anthropic + Gemini)
  Tools/                     # KSP game data and domain knowledge
    ToolDefinitions.cs       # ToolSpec list (name, description, parameters JSON) → emits OpenAI (GetToolsJsonArray), Anthropic (GetToolsJsonArrayAnthropic), and Gemini (GetToolsJsonArrayGemini, wrapped in functionDeclarations) tool array formats; dispatch to GameDataTools
    GameDataTools.cs         # KSP game data queries, vessel capability analysis (vessel parts, celestial bodies, contracts, Δv analysis, etc.)
    SkillDefinitions.cs      # Skill struct, frontmatter parser, lazy file loader from GameData/Kerpilot/Skills/
    SkillSelector.cs         # Composes system prompt with all skill content; LLM decides relevance
  Skills/                    # Source .md skill files (copied to GameData/Kerpilot/Skills/ at build time)
    basic_game_control.md    # Keyboard controls, SAS/RCS, time warp, camera, EVA, editor
    orbital_mechanics.md     # Patched conics, burn directions, Hohmann transfers, gravity turns, rendezvous
    rocket_design.md         # Staging, TWR, Tsiolkovsky equation, aerodynamics, engine selection
    delta_v_budget.md        # Δv estimation, budgeting tips, transfer planning
    contracts_guide.md       # Contract types, parameter requirements, economy advice
GameData/Kerpilot/
  Plugins/                   # Deployed DLL (build output, symlinked into KSP GameData)
  Skills/                    # Deployed skill .md files (build output, copied from src/Skills/)
  PluginData/settings.cfg    # User settings (created at runtime, not committed)
tests/
  Kerpilot.Tests.csproj      # NUnit test project (net472, references main project + KSP DLLs)
  ToolAvailabilityTests.cs   # Tests for tool definitions, dispatch, JSON parsing, request body
  SkillTests.cs              # Tests for skill definitions, prompt composition, frontmatter parsing
```

Key design decisions:
- All UI is built programmatically via uGUI (no asset bundles, no OnGUI/IMGUI)
- **Terminal-style interface**: All messages rendered in a **single rich-text `Text` component** (`_logText`) using `<color>` tags. User messages prefixed with green `> `, AI responses indented in white, tool status in italic amber. Input is an inline row below the log text — like typing in a real console. Old messages are auto-trimmed at ~14k chars (Unity Text vertex limit). `FormatLine()` is the shared formatter; color hex constants live in `UIStyleConstants`.
- Rounded-rect sprites generated at runtime with 9-slice via `SpriteFactory` (used by settings panel input fields)
- **LLM streaming**: Uses `UnityWebRequest` with a custom `DownloadHandlerScript` subclass (`SseDownloadHandler`) to parse SSE chunks and accumulate tool call fragments. The handler takes a `Func<string, IEnumerable<StreamDelta>>` parser delegate so the same plumbing serves all three providers — `JsonHelper.ParseStreamDelta` for OpenAI (walks brace/string structure to locate `choices[0].delta` then extracts `content`/`tool_calls` within that scope; avoids false matches in metadata-only chunks like OpenRouter usage/cost or Gemini finish chunks), `JsonHelper.ParseAnthropicStreamEvent` for Anthropic (routes by top-level `type`: `content_block_start` with `tool_use`, `content_block_delta` with `text_delta`/`input_json_delta`; ignores `message_start`/`message_stop`/`ping`), and `JsonHelper.ParseGeminiStreamEvents` for Gemini (iterates `candidates[0].content.parts[]`, emitting one StreamDelta per part — `text` populates Content; `functionCall` populates Name + raw-JSON `args` object as Arguments + synthesized id + the part-level `thoughtSignature` field on `ToolCall.ThoughtSignature` (set by thinking-enabled models like Gemini 2.5; must round-trip verbatim or the API 400s with "Function call is missing a thought_signature"), with ToolCallIndex = part position so parallel calls in one chunk get distinct slots). Single-delta parsers are wrapped via `LlmClient.WrapSingle` to satisfy the multi-delta delegate type. Tool call slots are stored in a `SortedDictionary<int, _>` keyed by parser-supplied index — for OpenAI that's the tool-call array index, for Anthropic the content-block index (which may interleave with text blocks), for Gemini the part index within the chunk's parts array (Gemini emits each functionCall complete in one chunk, so per-chunk indices suffice). No JSON library dependency — KSP's bundled Mono lacks `System.Xml.Linq`, which causes Newtonsoft.Json to fail type-load at addon-instantiation time. `StreamingUiLoop` coroutine drives a typewriter effect, throttled to ~10fps with change detection to avoid layout rebuild spam. During streaming, `_logBuilder.Length` snapshots enable efficient rollback without string copies. The final `ChatMessage` is created on completion.
- **Multi-provider request shaping**: `LlmClient.SendChatRequest` calls `ChatProviderDetector.Detect(BaseUrl)` to pick the protocol. OpenAI: POSTs to `{base}/chat/completions` with `Authorization: Bearer ...` and the standard tool-array body. Anthropic: POSTs to `{base}[/v1]/messages` (resolved by `BuildAnthropicUrl` — preserves an existing `/v1` suffix, otherwise appends `/v1/messages`) with `x-api-key` + `anthropic-version: 2023-06-01`, system prompt at top level (not as a message), `max_tokens: 4096`, tools using `input_schema`, assistant tool calls emitted as `content` blocks mixing `text` + `tool_use` (with the internal Arguments JSON string inlined raw as the `input` object), and consecutive tool results coalesced into a single user message of `tool_result` blocks. Gemini: POSTs to `{base}[/v1beta]/models/{model}:streamGenerateContent?alt=sse` (resolved by `BuildGeminiUrl` — preserves `/v1` or `/v1beta`, otherwise appends `/v1beta`) with `x-goog-api-key`, system prompt hoisted to a top-level `systemInstruction.parts[].text` field (Gemini has no system role), `assistant` role rewritten as `model`, tools wrapped in a single `[{functionDeclarations: [...]}]`, assistant tool calls emitted as `model` parts mixing `text` + `functionCall` (with stored Arguments JSON inlined raw as the `args` object, and `ToolCall.ThoughtSignature` re-emitted verbatim as a sibling `thoughtSignature` field on the same part when present), and tool results emitted as `user` parts containing `functionResponse` blocks keyed by **function name** (not by call id — Gemini doesn't propagate ids), with consecutive tool messages coalesced into a single user turn to satisfy Gemini's strict user/model alternation. `ChatMessage.ToolName` carries the function name on tool-result messages so it can be re-emitted in the functionResponse block; tool-call ids are synthesized client-side as `gemini_call_{partIndex}_{name}` so the existing accumulator path is unchanged.
- **Tool calling (function calling)**: Supports OpenAI-compatible tool use. `ToolDefinitions` provides 11 tool JSON schemas and dispatches to `GameDataTools` which queries KSP APIs (`FlightGlobals`, `EditorLogic`, `PartLoader`, `CelestialBody`, `ContractSystem`, `ResearchAndDevelopment`). `ChatWindow.StreamLlmResponse` runs a multi-round coroutine loop (max 5): if the LLM responds with `tool_calls`, tools are executed synchronously and results sent back until the LLM produces a text response. Core vessel tools (`get_vessel_parts`, `get_vessel_delta_v`, `analyze_vessel`) work in both flight and VAB/SPH editor via `TryGetShipParts`/`TryGetDeltaV` helpers that auto-detect the scene; orbit/status/list tools remain flight-only. Per-tool status labels (e.g. "Calculating delta-v...") are shown as plain italic text during execution.
- **Skills (domain knowledge injection)**: Skills are `.md` files in `src/Skills/` with YAML-like frontmatter (`id`, `title`, `description`) and markdown body content, copied to `GameData/Kerpilot/Skills/` at build time. `SkillDefinitions` lazily loads and caches all `*.md` files from the deployed directory on first access, parsing frontmatter via simple string splitting. All skills are included in the system prompt with their descriptions — the LLM decides which knowledge is relevant to the conversation. `SkillSelector.ComposeSystemPrompt` assembles the base prompt plus all skill content. Users can add custom skills by dropping `.md` files in the Skills directory.
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

NUnit test suite (`tests/GeminiProviderTests.cs`) verifies the native Gemini path:
- **Detector**: `generativelanguage.googleapis.com` → Gemini, `/openai` suffix → OpenAI fallback, `/gemini` proxy suffix → Gemini, Anthropic detection unchanged
- **URL builder**: `BuildGeminiUrl` appends `/v1beta` when missing, preserves existing `/v1`/`/v1beta`, always emits `:streamGenerateContent?alt=sse`
- **Tools array**: `GetToolsJsonArrayGemini` wraps in `functionDeclarations`, uses `parameters` (not `input_schema`), no `type:function` wrapper
- **Request body**: hoists system prompt to `systemInstruction`, rewrites assistant→model, emits `functionCall`/`functionResponse` blocks, coalesces consecutive tool results into one user turn, wraps non-object tool results under `{"content": "..."}`
- **Stream parser**: extracts text and functionCall parts, preserves args object verbatim, captures part-level `thoughtSignature` on functionCall parts, emits distinct ToolCallIndex for parallel functionCalls, returns empty list for usage-only chunks and null/empty input
- **Thought signature round-trip**: parser captures `thoughtSignature` on functionCall parts; request builder re-emits it as a sibling field on the same part; omits the field when no signature was captured
- **ChatMessage**: `CreateToolResult` accepts optional `toolName`

NUnit test suite (`tests/ToolAvailabilityTests.cs`) verifies tool infrastructure without requiring a running KSP instance:
- **Tool definitions**: All 11 tools present in JSON array, each with description and parameters schema, required parameters correct
- **Status labels**: Every tool name maps to a non-empty label ending in "..."
- **ExecuteTool dispatch**: Unknown tools return error JSON with escaped names; missing required params return errors (not exceptions)
- **JsonHelper parsing**: `ExtractJsonStringValue` for tool arguments, SSE `tool_calls` detection/extraction (index, id, function name, arguments fragments)
- **ChatMessage model**: `ToolCall` properties, `CreateAssistantToolCall`/`CreateToolResult` factory methods
- **Request body**: `BuildChatRequestBody` includes tools JSON, serializes tool call history correctly, omits tools field when null

NUnit test suite (`tests/SkillTests.cs`) verifies the skill system:
- **Prompt composition**: All 5 skills included in system prompt with titles, descriptions, and content; base prompt unchanged when no skills loaded
- **Frontmatter parsing**: Extracts id, title, description, content; handles Windows line endings, multiline content, missing/empty input

## Rules

1. **Rebuild after code changes**: After adding or modifying code, always run `dotnet build -c Release` and verify 0 errors before proceeding.
2. **Update README.md on new features**: When adding new user-facing functionality, update README.md to reflect the changes.
3. **Update CLAUDE.md on architecture changes**: When the project structure, build system, or class responsibilities change, update this file to stay accurate.
4. **Run /simplify before commit**: Before committing, run `/simplify` to review changed code for reuse, quality, and efficiency.
