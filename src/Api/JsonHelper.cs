using System.Collections.Generic;
using System.Text;

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
        /// Recursive depth-first search for the first occurrence of "key": "value"
        /// anywhere in the JSON. Used for tool argument parsing and error-message
        /// extraction from arbitrary API response shapes. Returns null if missing or
        /// the value is null.
        /// </summary>
        public static string ExtractJsonStringValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;

            string searchKey = "\"" + key + "\"";
            int keyIndex = json.IndexOf(searchKey);
            if (keyIndex < 0) return null;

            int colon = json.IndexOf(':', keyIndex + searchKey.Length);
            if (colon < 0) return null;
            return ExtractStringValueAt(json, colon, json.Length);
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

            sb.Append("{\"role\":\"system\",\"content\":\"");
            sb.Append(EscapeJsonString(systemPrompt));
            sb.Append("\"}");

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
                    {
                        sb.Append("null");
                    }
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
        /// Parses an SSE delta payload by walking braces to locate
        /// choices[0].delta, then extracting content / tool_calls only within that
        /// scope. This avoids false matches in metadata-only chunks (OpenRouter
        /// usage/cost, Gemini finish chunks, empty-choices payloads) that mention
        /// "content" or "tool_calls" outside the delta object. Returns null only
        /// for null/empty input; otherwise an empty StreamDelta if the payload
        /// has no choices[0].delta.
        /// </summary>
        public static StreamDelta ParseStreamDelta(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var result = new StreamDelta();

            int rootStart = json.IndexOf('{');
            if (rootStart < 0) return result;
            int rootEnd = FindMatchingClose(json, rootStart);
            if (rootEnd < 0) return result;

            int choicesArrStart, choicesArrEnd;
            if (!FindArrayValue(json, rootStart + 1, rootEnd, "choices", out choicesArrStart, out choicesArrEnd))
                return result;

            int firstChoice = SkipWhitespace(json, choicesArrStart + 1);
            if (firstChoice >= choicesArrEnd || json[firstChoice] != '{') return result;
            int firstChoiceEnd = FindMatchingClose(json, firstChoice);
            if (firstChoiceEnd < 0) return result;

            int deltaStart, deltaEnd;
            if (!FindObjectValue(json, firstChoice + 1, firstChoiceEnd, "delta", out deltaStart, out deltaEnd))
                return result;

            int contentColon = FindKeyAtTopLevel(json, deltaStart + 1, deltaEnd, "content");
            if (contentColon >= 0)
                result.Content = ExtractStringValueAt(json, contentColon, deltaEnd);

            int tcArrStart, tcArrEnd;
            if (!FindArrayValue(json, deltaStart + 1, deltaEnd, "tool_calls", out tcArrStart, out tcArrEnd))
                return result;

            int firstTc = SkipWhitespace(json, tcArrStart + 1);
            if (firstTc >= tcArrEnd || json[firstTc] != '{') return result;
            int firstTcEnd = FindMatchingClose(json, firstTc);
            if (firstTcEnd < 0) return result;

            result.HasToolCalls = true;

            int idxColon = FindKeyAtTopLevel(json, firstTc + 1, firstTcEnd, "index");
            if (idxColon >= 0)
                result.ToolCallIndex = ExtractIntValueAt(json, idxColon, firstTcEnd, 0);

            int idColon = FindKeyAtTopLevel(json, firstTc + 1, firstTcEnd, "id");
            if (idColon >= 0)
                result.ToolCallId = ExtractStringValueAt(json, idColon, firstTcEnd);

            int fnStart, fnEnd;
            if (FindObjectValue(json, firstTc + 1, firstTcEnd, "function", out fnStart, out fnEnd))
            {
                int nameColon = FindKeyAtTopLevel(json, fnStart + 1, fnEnd, "name");
                if (nameColon >= 0)
                    result.ToolCallFunctionName = ExtractStringValueAt(json, nameColon, fnEnd);

                int argsColon = FindKeyAtTopLevel(json, fnStart + 1, fnEnd, "arguments");
                if (argsColon >= 0)
                    result.ToolCallArguments = ExtractStringValueAt(json, argsColon, fnEnd);
            }

            return result;
        }

        private static bool FindArrayValue(string s, int start, int end, string key, out int arrStart, out int arrEnd)
        {
            return FindContainerValue(s, start, end, key, '[', out arrStart, out arrEnd);
        }

        private static bool FindObjectValue(string s, int start, int end, string key, out int objStart, out int objEnd)
        {
            return FindContainerValue(s, start, end, key, '{', out objStart, out objEnd);
        }

        private static bool FindContainerValue(string s, int start, int end, string key, char openChar, out int valStart, out int valEnd)
        {
            valStart = valEnd = -1;
            int colon = FindKeyAtTopLevel(s, start, end, key);
            if (colon < 0) return false;
            int p = SkipWhitespace(s, colon + 1);
            if (p >= end || s[p] != openChar) return false;
            int matched = FindMatchingClose(s, p);
            if (matched < 0 || matched >= end) return false;
            valStart = p;
            valEnd = matched;
            return true;
        }

        private static int FindKeyAtTopLevel(string s, int start, int end, string key)
        {
            string searchKey = "\"" + key + "\"";
            int keyLen = searchKey.Length;
            int depth = 0;
            int i = start;
            while (i < end)
            {
                char c = s[i];
                if (c == '"')
                {
                    if (depth == 0 && i + keyLen <= end &&
                        string.CompareOrdinal(s, i, searchKey, 0, keyLen) == 0)
                    {
                        int colonPos = SkipWhitespace(s, i + keyLen);
                        if (colonPos < end && s[colonPos] == ':') return colonPos;
                    }
                    i = SkipString(s, i, end);
                    continue;
                }
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
                i++;
            }
            return -1;
        }

        private static int FindMatchingClose(string s, int openIndex)
        {
            char open = s[openIndex];
            char close = open == '{' ? '}' : ']';
            int depth = 1;
            int i = openIndex + 1;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"') { i = SkipString(s, i, s.Length); continue; }
                if (c == open) depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0) return i;
                }
                i++;
            }
            return -1;
        }

        private static int SkipString(string s, int quoteIndex, int end)
        {
            int i = quoteIndex + 1;
            while (i < end)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < end) { i += 2; continue; }
                if (c == '"') return i + 1;
                i++;
            }
            return end;
        }

        private static int SkipWhitespace(string s, int p)
        {
            while (p < s.Length && (s[p] == ' ' || s[p] == '\t' || s[p] == '\n' || s[p] == '\r')) p++;
            return p;
        }

        private static string ExtractStringValueAt(string s, int colonPos, int end)
        {
            int p = SkipWhitespace(s, colonPos + 1);
            if (p >= end) return null;
            if (s[p] != '"') return null;

            var sb = new StringBuilder();
            int i = p + 1;
            while (i < end)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < end)
                {
                    char next = s[i + 1];
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
                else if (c == '"') return sb.ToString();
                else { sb.Append(c); i++; }
            }
            return sb.ToString();
        }

        private static int ExtractIntValueAt(string s, int colonPos, int end, int fallback)
        {
            int p = SkipWhitespace(s, colonPos + 1);
            if (p >= end) return fallback;
            bool neg = false;
            if (s[p] == '-') { neg = true; p++; }
            if (p >= end || s[p] < '0' || s[p] > '9') return fallback;
            int val = 0;
            while (p < end && s[p] >= '0' && s[p] <= '9')
            {
                val = val * 10 + (s[p] - '0');
                p++;
            }
            return neg ? -val : val;
        }
    }
}
