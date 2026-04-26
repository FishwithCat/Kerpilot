using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Kerpilot
{
    public static class LlmClient
    {
        public const string BaseSystemPrompt =
            "You are Kerpilot, an AI assistant for Kerbal Space Program. " +
            "Help the player with orbital mechanics, rocket design, mission planning, and gameplay tips. " +
            "Keep responses concise and practical.\n\n" +
            "CRITICAL: Never guess, estimate, or recall from memory any in-game numerical values " +
            "(delta-v, mass, thrust, Isp, gravity, atmosphere pressure, orbital parameters, body radius, " +
            "resource amounts, part stats, etc.). A live game-state snapshot is attached each turn under " +
            "'## Current Game State' — use those values directly without a tool call. For anything not in " +
            "the snapshot, ALWAYS use the available tools to query actual game data first. " +
            "This applies even when you think you know the value — the player's game state, mods, or configs " +
            "may differ from defaults. If a tool is available to retrieve the data, you must call it before " +
            "referencing any numbers in your response.\n\n" +
            "When the player asks you to design or recommend a rocket configuration, ALWAYS call " +
            "get_contracts first to check their current mission objectives. Design the most " +
            "economical build that satisfies those contract requirements — minimize part count, cost, " +
            "and complexity while ensuring sufficient delta-v and TWR. Only suggest capabilities " +
            "beyond the contract requirements if the player explicitly asks for them.";

        private const int AnthropicMaxTokens = 4096;
        private const string AnthropicVersion = "2023-06-01";

        public static IEnumerator SendChatRequest(
            List<ChatMessage> history,
            KerpilotSettings settings,
            Action<string> onToken,
            Action<string> onComplete,
            Action<List<ToolCall>, List<string>> onToolCalls,
            Action<string> onError,
            Func<bool> isCancelled = null)
        {
            if (!settings.IsConfigured)
            {
                onError?.Invoke("API key not configured. Open Settings to set your API key.");
                yield break;
            }

            ChatProvider provider = ChatProviderDetector.Detect(settings.BaseUrl);
            string gameState = GameDataTools.GetGameStateSnapshot();
            string systemPrompt = SkillSelector.ComposeSystemPrompt(BaseSystemPrompt, gameState);

            string url;
            string body;
            Func<string, IEnumerable<StreamDelta>> parser;
            if (provider == ChatProvider.Anthropic)
            {
                url = BuildAnthropicUrl(settings.BaseUrl);
                body = JsonHelper.BuildAnthropicRequestBody(
                    history, settings.ModelName, systemPrompt,
                    ToolDefinitions.GetToolsJsonArrayAnthropic(), AnthropicMaxTokens);
                parser = WrapSingle(JsonHelper.ParseAnthropicStreamEvent);
            }
            else if (provider == ChatProvider.Gemini)
            {
                url = BuildGeminiUrl(settings.BaseUrl, settings.ModelName);
                body = JsonHelper.BuildGeminiRequestBody(
                    history, settings.ModelName, systemPrompt,
                    ToolDefinitions.GetToolsJsonArrayGemini());
                parser = JsonHelper.ParseGeminiStreamEvents;
            }
            else
            {
                url = settings.BaseUrl.TrimEnd('/') + "/chat/completions";
                body = JsonHelper.BuildChatRequestBody(
                    history, settings.ModelName, systemPrompt,
                    ToolDefinitions.GetToolsJsonArray());
                parser = WrapSingle(JsonHelper.ParseStreamDelta);
            }

            var request = new UnityWebRequest(url, "POST");
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            // KSP's bundled Unity HTTP stack rejects many modern TLS chains
            // ("Unable to complete SSL connection"). Trust depends on the
            // user-supplied Base URL + API key; degrade chain validation
            // rather than block all requests.
            request.certificateHandler = new PermissiveCertificateHandler();
            request.disposeCertificateHandlerOnDispose = true;
            request.SetRequestHeader("Content-Type", "application/json");
            if (provider == ChatProvider.Anthropic)
            {
                request.SetRequestHeader("x-api-key", settings.ApiKey);
                request.SetRequestHeader("anthropic-version", AnthropicVersion);
            }
            else if (provider == ChatProvider.Gemini)
            {
                request.SetRequestHeader("x-goog-api-key", settings.ApiKey);
            }
            else
            {
                request.SetRequestHeader("Authorization", "Bearer " + settings.ApiKey);
            }

            var streamHandler = new SseDownloadHandler(parser);
            request.downloadHandler = streamHandler;

            request.SendWebRequest();

            var accumulated = new StringBuilder();
            float lastActivityTime = Time.unscaledTime;
            const float inactivityTimeout = 30f;
            bool timedOut = false;

            while (!request.isDone)
            {
                float now = Time.unscaledTime;
                string token = streamHandler.ConsumeTokens();
                if (!string.IsNullOrEmpty(token))
                {
                    accumulated.Append(token);
                    onToken?.Invoke(accumulated.ToString());
                    lastActivityTime = now;
                }
                else if (streamHandler.ConsumeNewDataFlag())
                {
                    lastActivityTime = now;
                }

                if (isCancelled != null && isCancelled())
                {
                    request.Abort();
                    request.Dispose();
                    yield break;
                }

                if (now - lastActivityTime > inactivityTimeout)
                {
                    timedOut = true;
                    request.Abort();
                    break;
                }

                yield return null;
            }

            // Flush any incomplete SSE line remaining in the buffer
            streamHandler.FlushBuffer();

            // Process any remaining tokens
            string remaining = streamHandler.ConsumeTokens();
            if (!string.IsNullOrEmpty(remaining))
            {
                accumulated.Append(remaining);
                onToken?.Invoke(accumulated.ToString());
            }

            if (timedOut && accumulated.Length > 0)
            {
                onComplete?.Invoke(accumulated.ToString());
            }
            else if (timedOut)
            {
                onError?.Invoke("Response timed out (no data received for " + (int)inactivityTimeout + "s).");
            }
            else if (request.isNetworkError || request.isHttpError)
            {
                string errorMsg;
                if (request.responseCode == 401)
                    errorMsg = "Authentication failed. Check your API key in Settings.";
                else if (request.responseCode == 429)
                    errorMsg = "Rate limit exceeded. Please wait and try again.";
                else if (request.responseCode == 404)
                    errorMsg = "API endpoint not found. Check your Base URL in Settings.";
                else if (request.isNetworkError)
                    errorMsg = "Network error: " + request.error;
                else
                {
                    string raw = streamHandler.GetRawResponse();
                    string apiError = !string.IsNullOrEmpty(raw)
                        ? JsonHelper.ExtractJsonStringValue(raw, "message")
                        : null;
                    errorMsg = apiError != null
                        ? "API error (" + request.responseCode + "): " + apiError
                        : "API error (" + request.responseCode + "): " + request.error;
                }

                if (accumulated.Length > 0)
                    onComplete?.Invoke(accumulated.ToString());
                else
                    onError?.Invoke(errorMsg);
            }
            else if (streamHandler.HasToolCalls)
            {
                onToolCalls?.Invoke(streamHandler.GetToolCalls(), streamHandler.GetPreservedContentBlocks());
            }
            else
            {
                string result = accumulated.ToString();
                if (string.IsNullOrEmpty(result))
                {
                    string raw = streamHandler.GetRawResponse();
                    string apiError = !string.IsNullOrEmpty(raw)
                        ? JsonHelper.ExtractJsonStringValue(raw, "message")
                        : null;
                    if (apiError != null)
                        onError?.Invoke("API error: " + apiError);
                    else if (!string.IsNullOrEmpty(raw))
                        onError?.Invoke("Unexpected API response (no streamed content). Check Base URL and model name.");
                    else
                        onError?.Invoke("Empty response from API. The server returned no data.");
                }
                else
                {
                    onComplete?.Invoke(result);
                }
            }

            request.Dispose();
        }

        /// <summary>
        /// Resolves the Anthropic Messages endpoint from a base URL.
        /// Accepts forms like "https://api.anthropic.com", ".../v1", or
        /// ".../anthropic" (proxy-style, e.g. DeepSeek).
        /// </summary>
        public static string BuildAnthropicUrl(string baseUrl)
        {
            string b = (baseUrl ?? "").TrimEnd('/');
            if (b.EndsWith("/v1") || b.EndsWith("/v1beta"))
                return b + "/messages";
            return b + "/v1/messages";
        }

        /// <summary>
        /// Resolves the Gemini :streamGenerateContent endpoint for a given
        /// model. Accepts base URLs like "https://generativelanguage.googleapis.com"
        /// (auto-appends /v1beta) or one already ending in /v1 or /v1beta.
        /// alt=sse forces server-sent event framing instead of one giant array.
        /// </summary>
        public static string BuildGeminiUrl(string baseUrl, string model)
        {
            string b = (baseUrl ?? "").TrimEnd('/');
            string suffix = "/models/" + model + ":streamGenerateContent?alt=sse";
            if (b.EndsWith("/v1") || b.EndsWith("/v1beta"))
                return b + suffix;
            return b + "/v1beta" + suffix;
        }

        private static Func<string, IEnumerable<StreamDelta>> WrapSingle(Func<string, StreamDelta> single)
        {
            return json =>
            {
                var d = single(json);
                return d == null ? null : new[] { d };
            };
        }
    }

    /// <summary>
    /// Always-trust certificate handler. KSP 1.12.5's bundled Unity HTTP stack
    /// often fails the TLS handshake against modern endpoints (api.openai.com,
    /// api.anthropic.com, etc.) because of outdated root CAs / cipher support
    /// in the embedded SSL implementation. Bypassing chain validation is the
    /// pragmatic fix for an LLM client where the user supplies the URL + key.
    /// </summary>
    public class PermissiveCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }

    /// <summary>
    /// SSE download handler shared by OpenAI, Anthropic and Gemini providers.
    /// The parser delegate decodes each "data: ..." payload into zero or more
    /// StreamDelta items. Tool call slots are keyed by parser-supplied index —
    /// OpenAI uses the tool_calls array index, Anthropic the content_block
    /// index, Gemini the position of the functionCall part within the chunk.
    /// </summary>
    public class SseDownloadHandler : DownloadHandlerScript
    {
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly StringBuilder _pendingTokens = new StringBuilder();
        private readonly StringBuilder _rawResponse = new StringBuilder();
        private readonly SortedDictionary<int, ToolCallAccumulator> _toolCalls = new SortedDictionary<int, ToolCallAccumulator>();
        private readonly SortedDictionary<int, PreservedBlockAccumulator> _preservedBlocks = new SortedDictionary<int, PreservedBlockAccumulator>();
        private readonly List<string> _preservedRawParts = new List<string>();
        private readonly Func<string, IEnumerable<StreamDelta>> _parser;
        private bool _hasNewData;

        private class ToolCallAccumulator
        {
            public string Id;
            public string Name;
            public string ThoughtSignature;
            public readonly StringBuilder Arguments = new StringBuilder();
        }

        private class PreservedBlockAccumulator
        {
            public string Type;                                 // "thinking" | "redacted_thinking"
            public readonly StringBuilder Text = new StringBuilder();
            public string Signature;
            public string Data;
        }

        public SseDownloadHandler(Func<string, IEnumerable<StreamDelta>> parser)
        {
            _parser = parser;
        }

        public bool HasToolCalls => _toolCalls.Count > 0;

        public bool ConsumeNewDataFlag()
        {
            bool val = _hasNewData;
            _hasNewData = false;
            return val;
        }

        public string GetRawResponse() => _rawResponse.ToString();

        public List<ToolCall> GetToolCalls()
        {
            var result = new List<ToolCall>();
            foreach (var kv in _toolCalls)
                result.Add(new ToolCall(
                    kv.Value.Id,
                    kv.Value.Name,
                    kv.Value.Arguments.ToString(),
                    kv.Value.ThoughtSignature));
            return result;
        }

        /// <summary>
        /// Returns serialized provider-opaque content blocks that must be passed
        /// back unchanged in the next request — Anthropic thinking + signature
        /// (and redacted_thinking) blocks. Returns null when there are none.
        /// </summary>
        public List<string> GetPreservedContentBlocks()
        {
            if (_preservedBlocks.Count == 0 && _preservedRawParts.Count == 0) return null;
            var result = new List<string>(_preservedBlocks.Count + _preservedRawParts.Count);
            foreach (var kv in _preservedBlocks)
            {
                var b = kv.Value;
                if (b.Type == "redacted_thinking")
                {
                    result.Add("{\"type\":\"redacted_thinking\",\"data\":\"" +
                        JsonHelper.EscapeJsonString(b.Data ?? "") + "\"}");
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.Append("{\"type\":\"thinking\",\"thinking\":\"");
                    sb.Append(JsonHelper.EscapeJsonString(b.Text.ToString()));
                    sb.Append("\",\"signature\":\"");
                    sb.Append(JsonHelper.EscapeJsonString(b.Signature ?? ""));
                    sb.Append("\"}");
                    result.Add(sb.ToString());
                }
            }
            // Gemini-only: raw parts captured verbatim from the stream, in
            // arrival order. The Gemini request builder emits these directly
            // as the assistant turn's parts array.
            result.AddRange(_preservedRawParts);
            return result;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
            _buffer.Append(chunk);
            if (_rawResponse.Length < 4096)
                _rawResponse.Append(chunk);
            _hasNewData = true;
            ProcessBuffer();
            return true;
        }

        public string ConsumeTokens()
        {
            if (_pendingTokens.Length == 0)
                return null;
            string tokens = _pendingTokens.ToString();
            _pendingTokens.Clear();
            return tokens;
        }

        private void ProcessBuffer()
        {
            string text = _buffer.ToString();
            int lastNewline = text.LastIndexOf('\n');
            if (lastNewline < 0) return;

            string complete = text.Substring(0, lastNewline + 1);
            _buffer.Clear();
            if (lastNewline + 1 < text.Length)
                _buffer.Append(text.Substring(lastNewline + 1));

            string[] lines = complete.Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("data: ")) continue;

                string payload = trimmed.Substring(6);
                if (payload == "[DONE]") continue;

                var deltas = _parser(payload);
                if (deltas == null) continue;

                foreach (var delta in deltas)
                {
                    if (delta == null) continue;

                    if (delta.PreservedRawJson != null)
                        _preservedRawParts.Add(delta.PreservedRawJson);

                    if (delta.HasPreservedBlock)
                        ProcessPreservedBlockDelta(delta);

                    if (delta.HasToolCalls)
                    {
                        ProcessToolCallDelta(delta);
                        if (delta.Content != null)
                            _pendingTokens.Append(delta.Content);
                        continue;
                    }

                    if (delta.Content != null)
                        _pendingTokens.Append(delta.Content);
                }
            }
        }

        private void ProcessToolCallDelta(StreamDelta delta)
        {
            if (!_toolCalls.TryGetValue(delta.ToolCallIndex, out var tc))
            {
                tc = new ToolCallAccumulator();
                _toolCalls[delta.ToolCallIndex] = tc;
            }

            if (delta.ToolCallId != null)
                tc.Id = delta.ToolCallId;
            if (delta.ToolCallFunctionName != null)
                tc.Name = delta.ToolCallFunctionName;
            if (delta.ToolCallArguments != null)
                tc.Arguments.Append(delta.ToolCallArguments);
            if (delta.ToolCallThoughtSignature != null)
                tc.ThoughtSignature = delta.ToolCallThoughtSignature;
        }

        private void ProcessPreservedBlockDelta(StreamDelta delta)
        {
            if (!_preservedBlocks.TryGetValue(delta.PreservedBlockIndex, out var b))
            {
                b = new PreservedBlockAccumulator();
                _preservedBlocks[delta.PreservedBlockIndex] = b;
            }

            if (delta.PreservedBlockType != null)
                b.Type = delta.PreservedBlockType;
            if (delta.PreservedBlockTextFragment != null)
                b.Text.Append(delta.PreservedBlockTextFragment);
            if (delta.PreservedBlockSignature != null)
                b.Signature = delta.PreservedBlockSignature;
            if (delta.PreservedBlockData != null)
                b.Data = delta.PreservedBlockData;
        }

        public void FlushBuffer()
        {
            if (_buffer.Length > 0)
            {
                _buffer.Append('\n');
                ProcessBuffer();
            }
        }

        protected override float GetProgress() => 0f;
    }
}
