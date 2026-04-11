using System.Collections.Generic;
using System.Text;

namespace Kerpilot
{
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
        /// Extracts a string value for a given key from a JSON object.
        /// Handles the predictable OpenAI chat completions response format.
        /// Returns null if the key is not found or the value is null.
        /// </summary>
        public static string ExtractJsonStringValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;

            string searchKey = "\"" + key + "\"";
            int keyIndex = json.IndexOf(searchKey);
            if (keyIndex < 0) return null;

            return ExtractJsonStringValueAt(json, keyIndex);
        }

        public static string BuildChatRequestBody(List<ChatMessage> history, string model, string systemPrompt, string toolsJson)
        {
            var sb = new StringBuilder();
            sb.Append("{\"model\":\"");
            sb.Append(EscapeJsonString(model));
            sb.Append("\",\"stream\":true");

            if (!string.IsNullOrEmpty(toolsJson))
            {
                sb.Append(",\"tools\":");
                sb.Append(toolsJson);
            }

            sb.Append(",\"messages\":[");

            // System message
            sb.Append("{\"role\":\"system\",\"content\":\"");
            sb.Append(EscapeJsonString(systemPrompt));
            sb.Append("\"}");

            // Conversation history (last 20 messages)
            int start = history.Count > 20 ? history.Count - 20 : 0;
            for (int i = start; i < history.Count; i++)
            {
                var msg = history[i];

                if (msg.Role == MessageRole.Tool)
                {
                    sb.Append(",{\"role\":\"tool\",\"tool_call_id\":\"");
                    sb.Append(EscapeJsonString(msg.ToolCallId));
                    sb.Append("\",\"content\":\"");
                    sb.Append(EscapeJsonString(msg.Text));
                    sb.Append("\"}");
                }
                else if (msg.Role == MessageRole.Assistant && msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    sb.Append(",{\"role\":\"assistant\",\"content\":");
                    if (string.IsNullOrEmpty(msg.Text))
                        sb.Append("null");
                    else
                    {
                        sb.Append("\"");
                        sb.Append(EscapeJsonString(msg.Text));
                        sb.Append("\"");
                    }
                    sb.Append(",\"tool_calls\":[");
                    for (int t = 0; t < msg.ToolCalls.Count; t++)
                    {
                        if (t > 0) sb.Append(",");
                        sb.Append("{\"id\":\"");
                        sb.Append(EscapeJsonString(msg.ToolCalls[t].Id));
                        sb.Append("\",\"type\":\"function\",\"function\":{\"name\":\"");
                        sb.Append(EscapeJsonString(msg.ToolCalls[t].FunctionName));
                        sb.Append("\",\"arguments\":\"");
                        sb.Append(EscapeJsonString(msg.ToolCalls[t].Arguments));
                        sb.Append("\"}}");
                    }
                    sb.Append("]}");
                }
                else
                {
                    sb.Append(",{\"role\":\"");
                    sb.Append(msg.Sender == MessageSender.User ? "user" : "assistant");
                    sb.Append("\",\"content\":\"");
                    sb.Append(EscapeJsonString(msg.Text));
                    sb.Append("\"}");
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// Checks if an SSE JSON payload contains tool_calls in the delta.
        /// </summary>
        public static bool HasToolCalls(string json)
        {
            if (json == null) return false;
            // Must check for "tool_calls":[  to distinguish from "tool_calls":null
            // which many APIs include in every delta chunk
            int idx = json.IndexOf("\"tool_calls\"");
            if (idx < 0) return false;
            // Skip past the key, colon, and whitespace to check the value
            int pos = json.IndexOf(':', idx + 12);
            if (pos < 0) return false;
            pos++;
            while (pos < json.Length && (json[pos] == ' ' || json[pos] == '\t'))
                pos++;
            // Must be a non-empty array: "[{" not just "[]"
            if (pos >= json.Length || json[pos] != '[') return false;
            pos++;
            while (pos < json.Length && (json[pos] == ' ' || json[pos] == '\t'))
                pos++;
            return pos < json.Length && json[pos] == '{';
        }

        /// <summary>
        /// Extracts tool call index from an SSE delta chunk.
        /// Returns -1 if not found.
        /// </summary>
        public static int ExtractToolCallIndex(string json)
        {
            // Find "tool_calls":[{"index":N
            int tcIdx = json.IndexOf("\"tool_calls\"");
            if (tcIdx < 0) return -1;

            int indexKey = json.IndexOf("\"index\"", tcIdx);
            if (indexKey < 0) return 0; // default to 0

            int colon = json.IndexOf(':', indexKey + 7);
            if (colon < 0) return 0;

            int start = colon + 1;
            while (start < json.Length && json[start] == ' ') start++;

            int result = 0;
            bool found = false;
            while (start < json.Length && json[start] >= '0' && json[start] <= '9')
            {
                result = result * 10 + (json[start] - '0');
                found = true;
                start++;
            }

            return found ? result : 0;
        }

        /// <summary>
        /// Extracts the tool call ID from an SSE delta chunk.
        /// </summary>
        public static string ExtractToolCallId(string json)
        {
            int tcIdx = json.IndexOf("\"tool_calls\"");
            if (tcIdx < 0) return null;
            // Find "id" after tool_calls
            int idIdx = json.IndexOf("\"id\"", tcIdx);
            if (idIdx < 0) return null;
            return ExtractJsonStringValueAt(json, idIdx);
        }

        /// <summary>
        /// Extracts the function name from an SSE delta tool_calls chunk.
        /// </summary>
        public static string ExtractToolCallFunctionName(string json)
        {
            int tcIdx = json.IndexOf("\"tool_calls\"");
            if (tcIdx < 0) return null;
            int fnIdx = json.IndexOf("\"function\"", tcIdx);
            if (fnIdx < 0) return null;
            int nameIdx = json.IndexOf("\"name\"", fnIdx);
            if (nameIdx < 0) return null;
            return ExtractJsonStringValueAt(json, nameIdx);
        }

        /// <summary>
        /// Extracts the function arguments fragment from an SSE delta tool_calls chunk.
        /// </summary>
        public static string ExtractToolCallArguments(string json)
        {
            int tcIdx = json.IndexOf("\"tool_calls\"");
            if (tcIdx < 0) return null;
            int fnIdx = json.IndexOf("\"function\"", tcIdx);
            if (fnIdx < 0) return null;
            int argIdx = json.IndexOf("\"arguments\"", fnIdx);
            if (argIdx < 0) return null;
            return ExtractJsonStringValueAt(json, argIdx);
        }

        /// <summary>
        /// Extract a JSON string value starting from a known key position.
        /// The keyIndex should point to the opening quote of the key.
        /// </summary>
        private static string ExtractJsonStringValueAt(string json, int keyIndex)
        {
            // Find closing quote of key
            int keyEnd = json.IndexOf('"', keyIndex + 1);
            if (keyEnd < 0) return null;

            int colon = json.IndexOf(':', keyEnd + 1);
            if (colon < 0) return null;

            int valueStart = colon + 1;
            while (valueStart < json.Length && (json[valueStart] == ' ' || json[valueStart] == '\t'))
                valueStart++;

            if (valueStart >= json.Length) return null;

            // Check for null
            if (valueStart + 4 <= json.Length && json.Substring(valueStart, 4) == "null")
                return null;

            if (json[valueStart] != '"') return null;

            var sb = new StringBuilder();
            int i = valueStart + 1;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        default: sb.Append(next); break;
                    }
                    i += 2;
                }
                else if (c == '"')
                {
                    break;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }
    }
}
