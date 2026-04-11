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
        private const string SystemPrompt =
            "You are Kerpilot, an AI assistant for Kerbal Space Program. " +
            "Help the player with orbital mechanics, rocket design, mission planning, and gameplay tips. " +
            "Keep responses concise and practical. " +
            "When the player asks about their vessel, contracts, or celestial bodies, use the available tools to get current game data.";

        public static IEnumerator SendChatRequest(
            List<ChatMessage> history,
            KerpilotSettings settings,
            Action<string> onToken,
            Action<string> onComplete,
            Action<List<ToolCall>> onToolCalls,
            Action<string> onError)
        {
            if (!settings.IsConfigured)
            {
                onError?.Invoke("API key not configured. Open Settings to set your API key.");
                yield break;
            }

            string url = settings.BaseUrl.TrimEnd('/') + "/chat/completions";
            string toolsJson = ToolDefinitions.GetToolsJsonArray();
            string body = JsonHelper.BuildChatRequestBody(history, settings.ModelName, SystemPrompt, toolsJson);

            var request = new UnityWebRequest(url, "POST");
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + settings.ApiKey);

            var streamHandler = new SseDownloadHandler();
            request.downloadHandler = streamHandler;

            request.SendWebRequest();

            var accumulated = new StringBuilder();

            while (!request.isDone)
            {
                string token = streamHandler.ConsumeTokens();
                if (!string.IsNullOrEmpty(token))
                {
                    accumulated.Append(token);
                    onToken?.Invoke(accumulated.ToString());
                }
                yield return null;
            }

            // Process any remaining tokens
            string remaining = streamHandler.ConsumeTokens();
            if (!string.IsNullOrEmpty(remaining))
            {
                accumulated.Append(remaining);
                onToken?.Invoke(accumulated.ToString());
            }

            if (request.isNetworkError || request.isHttpError)
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
                    // Include API error details from the response body for debugging
                    string rawBody = streamHandler.GetRawResponse();
                    string detail = null;
                    if (!string.IsNullOrEmpty(rawBody))
                        detail = JsonHelper.ExtractJsonStringValue(rawBody, "message")
                              ?? JsonHelper.ExtractJsonStringValue(rawBody, "error");
                    if (!string.IsNullOrEmpty(detail))
                        errorMsg = "API error (" + request.responseCode + "): " + detail;
                    else
                        errorMsg = "API error (" + request.responseCode + "): " + request.error;
                }

                // If we got some tokens before the error, still complete with what we have
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
                    result = "(Empty response from API)";
                onComplete?.Invoke(result);
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
        private readonly StringBuilder _rawResponse = new StringBuilder();
        private readonly StringBuilder _pendingTokens = new StringBuilder();
        private readonly List<ToolCallAccumulator> _toolCalls = new List<ToolCallAccumulator>();
        private bool _hasToolCalls;

        private class ToolCallAccumulator
        {
            public string Id;
            public string Name;
            public readonly StringBuilder Arguments = new StringBuilder();
        }

        public bool HasToolCalls => _hasToolCalls;

        /// <summary>
        /// Returns the raw received data (useful for reading error response bodies).
        /// </summary>
        public string GetRawResponse()
        {
            return _rawResponse.ToString();
        }

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
            _rawResponse.Append(chunk);
            _buffer.Append(chunk);
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

        protected override float GetProgress() => 0f;
    }
}
