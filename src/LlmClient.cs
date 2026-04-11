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
            "Keep responses concise and practical.";

        public static IEnumerator SendChatRequest(
            List<ChatMessage> history,
            KerpilotSettings settings,
            Action<string> onToken,
            Action<string> onComplete,
            Action<string> onError)
        {
            if (!settings.IsConfigured)
            {
                onError?.Invoke("API key not configured. Open Settings to set your API key.");
                yield break;
            }

            string url = settings.BaseUrl.TrimEnd('/') + "/chat/completions";
            string body = JsonHelper.BuildChatRequestBody(history, settings.ModelName, SystemPrompt);

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
                    errorMsg = "API error (" + request.responseCode + "): " + request.error;

                // If we got some tokens before the error, still complete with what we have
                if (accumulated.Length > 0)
                    onComplete?.Invoke(accumulated.ToString());
                else
                    onError?.Invoke(errorMsg);
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
        private readonly StringBuilder _pendingTokens = new StringBuilder();

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
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

                string content = JsonHelper.ExtractJsonStringValue(payload, "content");
                if (content != null)
                    _pendingTokens.Append(content);
            }
        }

        protected override float GetProgress() => 0f;
    }
}
