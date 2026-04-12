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
            "resource amounts, part stats, etc.). ALWAYS use the available tools to query actual game data first. " +
            "This applies even when you think you know the value — the player's game state, mods, or configs " +
            "may differ from defaults. If a tool is available to retrieve the data, you must call it before " +
            "referencing any numbers in your response.\n\n" +
            "When the player asks you to design or recommend a rocket configuration, ALWAYS call " +
            "get_contracts first to check their current mission objectives. Design the most " +
            "economical build that satisfies those contract requirements — minimize part count, cost, " +
            "and complexity while ensuring sufficient delta-v and TWR. Only suggest capabilities " +
            "beyond the contract requirements if the player explicitly asks for them.";

        public static IEnumerator SendChatRequest(
            List<ChatMessage> history,
            KerpilotSettings settings,
            Action<string> onToken,
            Action<string> onComplete,
            Action<List<ToolCall>> onToolCalls,
            Action<string> onError,
            Func<bool> isCancelled = null)
        {
            if (!settings.IsConfigured)
            {
                onError?.Invoke("API key not configured. Open Settings to set your API key.");
                yield break;
            }

            string url = settings.BaseUrl.TrimEnd('/') + "/chat/completions";
            string toolsJson = ToolDefinitions.GetToolsJsonArray();

            string systemPrompt = SkillSelector.ComposeSystemPrompt(BaseSystemPrompt);

            string body = JsonHelper.BuildChatRequestBody(history, settings.ModelName, systemPrompt, toolsJson);

            var request = new UnityWebRequest(url, "POST");
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + settings.ApiKey);

            var streamHandler = new SseDownloadHandler();
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
                // Timed out but we have partial content — use it
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
                    errorMsg = "API error (" + request.responseCode + "): " + request.error;

                if (accumulated.Length > 0)
                    onComplete?.Invoke(accumulated.ToString());
                else
                    onError?.Invoke(errorMsg);
            }
            else if (streamHandler.HasToolCalls)
            {
                onToolCalls?.Invoke(streamHandler.GetToolCalls());
            }
            else
            {
                string result = accumulated.ToString();
                if (string.IsNullOrEmpty(result))
                {
                    // Try to extract an error message from the raw response
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
    }

    /// <summary>
    /// Custom DownloadHandler that processes SSE (Server-Sent Events) streaming responses.
    /// Parses "data: {...}" lines and extracts delta content tokens.
    /// </summary>
    public class SseDownloadHandler : DownloadHandlerScript
    {
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly StringBuilder _pendingTokens = new StringBuilder();
        private readonly StringBuilder _rawResponse = new StringBuilder();
        private readonly List<ToolCallAccumulator> _toolCalls = new List<ToolCallAccumulator>();
        private bool _hasToolCalls;
        private bool _hasNewData;

        private class ToolCallAccumulator
        {
            public string Id;
            public string Name;
            public readonly StringBuilder Arguments = new StringBuilder();
        }

        public bool HasToolCalls => _hasToolCalls;

        /// <summary>Returns true if any data was received since the last call. Resets the flag.</summary>
        public bool ConsumeNewDataFlag()
        {
            bool val = _hasNewData;
            _hasNewData = false;
            return val;
        }

        /// <summary>Returns the raw response text (for error diagnostics when no SSE content was parsed).</summary>
        public string GetRawResponse() => _rawResponse.ToString();

        public List<ToolCall> GetToolCalls()
        {
            var result = new List<ToolCall>();
            foreach (var tc in _toolCalls)
                result.Add(new ToolCall(tc.Id, tc.Name, tc.Arguments.ToString()));
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

            // Process complete lines, keep incomplete data in buffer
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

                // Check for tool calls in this delta
                if (JsonHelper.HasToolCalls(payload))
                {
                    _hasToolCalls = true;
                    ProcessToolCallDelta(payload);
                    continue;
                }

                string content = JsonHelper.ExtractJsonStringValue(payload, "content");
                if (content != null)
                    _pendingTokens.Append(content);
            }
        }

        private void ProcessToolCallDelta(string payload)
        {
            int index = JsonHelper.ExtractToolCallIndex(payload);

            // Ensure we have enough slots
            while (_toolCalls.Count <= index)
                _toolCalls.Add(new ToolCallAccumulator());

            var tc = _toolCalls[index];

            // ID and name arrive in the first chunk for this tool call
            string id = JsonHelper.ExtractToolCallId(payload);
            if (id != null)
                tc.Id = id;

            string name = JsonHelper.ExtractToolCallFunctionName(payload);
            if (name != null)
                tc.Name = name;

            // Arguments are streamed incrementally
            string argFragment = JsonHelper.ExtractToolCallArguments(payload);
            if (argFragment != null)
                tc.Arguments.Append(argFragment);
        }

        /// <summary>
        /// Flush any remaining data in the buffer that didn't end with a newline.
        /// Must be called after the request completes to avoid losing the last SSE line.
        /// </summary>
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
