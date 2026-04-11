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

            // Find the colon after the key
            int colonIndex = json.IndexOf(':', keyIndex + searchKey.Length);
            if (colonIndex < 0) return null;

            // Skip whitespace after colon
            int valueStart = colonIndex + 1;
            while (valueStart < json.Length && (json[valueStart] == ' ' || json[valueStart] == '\t'))
                valueStart++;

            if (valueStart >= json.Length) return null;

            // Check for null
            if (valueStart + 4 <= json.Length && json.Substring(valueStart, 4) == "null")
                return null;

            // Expect a quoted string
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

        public static string BuildChatRequestBody(List<ChatMessage> history, string model, string systemPrompt)
        {
            var sb = new StringBuilder();
            sb.Append("{\"model\":\"");
            sb.Append(EscapeJsonString(model));
            sb.Append("\",\"stream\":true,\"messages\":[");

            // System message
            sb.Append("{\"role\":\"system\",\"content\":\"");
            sb.Append(EscapeJsonString(systemPrompt));
            sb.Append("\"}");

            // Conversation history (last 20 messages)
            int start = history.Count > 20 ? history.Count - 20 : 0;
            for (int i = start; i < history.Count; i++)
            {
                sb.Append(",{\"role\":\"");
                sb.Append(history[i].Sender == MessageSender.User ? "user" : "assistant");
                sb.Append("\",\"content\":\"");
                sb.Append(EscapeJsonString(history[i].Text));
                sb.Append("\"}");
            }

            sb.Append("]}");
            return sb.ToString();
        }
    }
}
