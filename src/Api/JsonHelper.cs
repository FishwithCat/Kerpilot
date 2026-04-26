using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kerpilot
{
    public class StreamDelta
    {
        public string Content;
        public bool HasToolCalls;
        public int ToolCallIndex;
        public string ToolCallId;
        public string ToolCallFunctionName;
        public string ToolCallArguments;
    }

    public static class JsonHelper
    {
        public static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Extracts the first string value matching the given key from a JSON object,
        /// searching recursively. Returns null if the key is missing or the value is null.
        /// </summary>
        public static string ExtractJsonStringValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonException) { return null; }

            var token = root.SelectToken("$.." + key);
            if (token == null || token.Type == JTokenType.Null) return null;
            return token.Type == JTokenType.String ? (string)token : token.ToString();
        }

        public static string BuildChatRequestBody(List<ChatMessage> history, string model, string systemPrompt, string toolsJson)
        {
            var root = new JObject
            {
                ["model"] = model,
                ["stream"] = true
            };

            if (!string.IsNullOrEmpty(toolsJson))
                root["tools"] = JArray.Parse(toolsJson);

            var messages = new JArray
            {
                new JObject
                {
                    ["role"] = "system",
                    ["content"] = systemPrompt
                }
            };

            int start = history.Count > 20 ? history.Count - 20 : 0;
            for (int i = start; i < history.Count; i++)
            {
                var msg = history[i];

                if (msg.Role == MessageRole.Tool)
                {
                    messages.Add(new JObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = msg.ToolCallId,
                        ["content"] = msg.Text
                    });
                }
                else if (msg.Role == MessageRole.Assistant && msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    var toolCallsArr = new JArray();
                    foreach (var tc in msg.ToolCalls)
                    {
                        toolCallsArr.Add(new JObject
                        {
                            ["id"] = tc.Id,
                            ["type"] = "function",
                            ["function"] = new JObject
                            {
                                ["name"] = tc.FunctionName,
                                ["arguments"] = tc.Arguments
                            }
                        });
                    }
                    messages.Add(new JObject
                    {
                        ["role"] = "assistant",
                        ["content"] = string.IsNullOrEmpty(msg.Text) ? JValue.CreateNull() : (JToken)msg.Text,
                        ["tool_calls"] = toolCallsArr
                    });
                }
                else
                {
                    messages.Add(new JObject
                    {
                        ["role"] = msg.Sender == MessageSender.User ? "user" : "assistant",
                        ["content"] = msg.Text
                    });
                }
            }

            root["messages"] = messages;
            return root.ToString(Formatting.None);
        }

        /// <summary>
        /// Parses an SSE delta payload into a StreamDelta. Looks specifically at
        /// choices[0].delta to avoid false matches in metadata-only chunks (usage,
        /// cost, provider info) that some providers (OpenRouter, Gemini) emit before
        /// or alongside content. Returns null if the payload isn't a valid JSON object.
        /// </summary>
        public static StreamDelta ParseStreamDelta(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonException) { return null; }

            var result = new StreamDelta();
            var choices = root["choices"] as JArray;
            if (choices == null || choices.Count == 0) return result;
            var delta = choices[0]["delta"];
            if (delta == null) return result;

            var contentToken = delta["content"];
            if (contentToken != null && contentToken.Type == JTokenType.String)
                result.Content = (string)contentToken;

            var toolCalls = delta["tool_calls"] as JArray;
            if (toolCalls == null || toolCalls.Count == 0) return result;

            result.HasToolCalls = true;
            var tc = toolCalls[0];
            result.ToolCallIndex = (int?)tc["index"] ?? 0;
            result.ToolCallId = (string)tc["id"];
            var fn = tc["function"];
            if (fn != null)
            {
                result.ToolCallFunctionName = (string)fn["name"];
                result.ToolCallArguments = (string)fn["arguments"];
            }
            return result;
        }
    }
}
