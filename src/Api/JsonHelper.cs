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

        // Gemini-only: opaque per-part signature on functionCall parts when
        // thinking is enabled. Must round-trip verbatim on the same part.
        public string ToolCallThoughtSignature;

        // Gemini-only: full raw JSON of a Part object captured verbatim from
        // the stream, so the assistant turn can be echoed back unchanged with
        // thoughtSignature/thought:true intact. Gemini's API rejects the next
        // request if signatures are stripped from any part — including thought
        // summary parts that precede a functionCall part.
        public string PreservedRawJson;

        // Anthropic extended-thinking content blocks. The block index is shared
        // with HasToolCalls/Content (Anthropic uses one content_block index space).
        public bool HasPreservedBlock;
        public int PreservedBlockIndex;
        public string PreservedBlockType;          // "thinking" or "redacted_thinking"
        public string PreservedBlockTextFragment;  // for thinking_delta
        public string PreservedBlockSignature;     // for signature_delta
        public string PreservedBlockData;          // for redacted_thinking start
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

        /// <summary>
        /// Builds an Anthropic Messages API request body.
        /// System prompt is a top-level field (not a message). Tool results are user
        /// messages with content-block arrays. Assistant messages with tool_calls are
        /// emitted as content-block arrays mixing text and tool_use blocks. The
        /// internally-stored Arguments JSON string is inlined as the tool_use input
        /// object (raw JSON).
        /// </summary>
        public static string BuildAnthropicRequestBody(
            List<ChatMessage> history, string model, string systemPrompt,
            string anthropicToolsJson, int maxTokens)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"model\":\"").Append(EscapeJsonString(model)).Append("\",");
            sb.Append("\"max_tokens\":").Append(maxTokens).Append(',');
            sb.Append("\"stream\":true");
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                sb.Append(",\"system\":\"").Append(EscapeJsonString(systemPrompt)).Append('"');
            }
            if (!string.IsNullOrEmpty(anthropicToolsJson))
            {
                sb.Append(",\"tools\":").Append(anthropicToolsJson);
            }
            sb.Append(",\"messages\":[");

            int start = history.Count > 20 ? history.Count - 20 : 0;
            bool firstMsg = true;
            int i = start;
            while (i < history.Count)
            {
                var msg = history[i];

                if (msg.Role == MessageRole.Tool)
                {
                    if (!firstMsg) sb.Append(',');
                    firstMsg = false;
                    sb.Append("{\"role\":\"user\",\"content\":[");
                    bool firstResult = true;
                    while (i < history.Count && history[i].Role == MessageRole.Tool)
                    {
                        if (!firstResult) sb.Append(',');
                        firstResult = false;
                        sb.Append("{\"type\":\"tool_result\",\"tool_use_id\":\"");
                        sb.Append(EscapeJsonString(history[i].ToolCallId));
                        sb.Append("\",\"content\":\"");
                        sb.Append(EscapeJsonString(history[i].Text));
                        sb.Append("\"}");
                        i++;
                    }
                    sb.Append("]}");
                    continue;
                }

                if (!firstMsg) sb.Append(',');
                firstMsg = false;

                if (msg.Role == MessageRole.Assistant && msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    sb.Append("{\"role\":\"assistant\",\"content\":[");
                    bool firstBlock = true;

                    // Preserved blocks (e.g. Anthropic thinking + signature) must be
                    // re-emitted unchanged before text/tool_use, or the API rejects
                    // the request when extended thinking is active.
                    if (msg.PreservedContentBlocks != null)
                    {
                        for (int p = 0; p < msg.PreservedContentBlocks.Count; p++)
                        {
                            string raw = msg.PreservedContentBlocks[p];
                            if (string.IsNullOrEmpty(raw)) continue;
                            if (!firstBlock) sb.Append(',');
                            firstBlock = false;
                            sb.Append(raw);
                        }
                    }

                    if (!string.IsNullOrEmpty(msg.Text))
                    {
                        if (!firstBlock) sb.Append(',');
                        firstBlock = false;
                        sb.Append("{\"type\":\"text\",\"text\":\"");
                        sb.Append(EscapeJsonString(msg.Text));
                        sb.Append("\"}");
                    }
                    for (int t = 0; t < msg.ToolCalls.Count; t++)
                    {
                        if (!firstBlock) sb.Append(',');
                        firstBlock = false;
                        var tc = msg.ToolCalls[t];
                        sb.Append("{\"type\":\"tool_use\",\"id\":\"");
                        sb.Append(EscapeJsonString(tc.Id));
                        sb.Append("\",\"name\":\"");
                        sb.Append(EscapeJsonString(tc.FunctionName));
                        sb.Append("\",\"input\":");
                        string args = tc.Arguments;
                        if (string.IsNullOrEmpty(args) || string.IsNullOrEmpty(args.Trim()))
                            sb.Append("{}");
                        else
                            sb.Append(args);
                        sb.Append('}');
                    }
                    sb.Append("]}");
                }
                else
                {
                    sb.Append("{\"role\":\"");
                    sb.Append(msg.Sender == MessageSender.User ? "user" : "assistant");
                    sb.Append("\",\"content\":\"");
                    sb.Append(EscapeJsonString(msg.Text));
                    sb.Append("\"}");
                }

                i++;
            }

            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// Builds a Gemini :streamGenerateContent request body.
        /// Roles are remapped: User → "user", Assistant → "model"; the system
        /// prompt is hoisted into a top-level "systemInstruction" field. Tool
        /// calls are emitted as "model" parts containing functionCall blocks
        /// (args object inlined raw from the stored Arguments JSON). Tool
        /// results — keyed by function name in Gemini, not by id — are emitted
        /// as "user" parts containing functionResponse blocks. Consecutive
        /// tool messages are coalesced into a single user turn so multi-tool
        /// rounds satisfy Gemini's strict user/model alternation.
        /// </summary>
        public static string BuildGeminiRequestBody(
            List<ChatMessage> history, string model, string systemPrompt, string geminiToolsJson)
        {
            var sb = new StringBuilder();
            sb.Append('{');

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                sb.Append("\"systemInstruction\":{\"parts\":[{\"text\":\"");
                sb.Append(EscapeJsonString(systemPrompt));
                sb.Append("\"}]},");
            }

            if (!string.IsNullOrEmpty(geminiToolsJson))
            {
                sb.Append("\"tools\":");
                sb.Append(geminiToolsJson);
                sb.Append(',');
            }

            sb.Append("\"contents\":[");

            int start = history.Count > 20 ? history.Count - 20 : 0;
            bool firstMsg = true;
            int i = start;
            while (i < history.Count)
            {
                var msg = history[i];

                if (msg.Role == MessageRole.Tool)
                {
                    if (!firstMsg) sb.Append(',');
                    firstMsg = false;
                    sb.Append("{\"role\":\"user\",\"parts\":[");
                    bool firstPart = true;
                    while (i < history.Count && history[i].Role == MessageRole.Tool)
                    {
                        if (!firstPart) sb.Append(',');
                        firstPart = false;
                        var t = history[i];
                        sb.Append("{\"functionResponse\":{\"name\":\"");
                        sb.Append(EscapeJsonString(t.ToolName ?? ""));
                        sb.Append("\",\"response\":");
                        AppendGeminiResponseObject(sb, t.Text);
                        sb.Append("}}");
                        i++;
                    }
                    sb.Append("]}");
                    continue;
                }

                if (!firstMsg) sb.Append(',');
                firstMsg = false;

                if (msg.Role == MessageRole.Assistant && msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    sb.Append("{\"role\":\"model\",\"parts\":[");

                    // When PreservedContentBlocks holds verbatim parts captured
                    // from the stream, echo them back unchanged — that is the
                    // ONLY way to satisfy Gemini's thought-signature roundtrip
                    // (signatures appear on thought-text parts that precede the
                    // functionCall, and Gemini 400s the next request if any are
                    // stripped). Skip the reconstructed text/functionCall path.
                    bool emittedPreserved = false;
                    if (msg.PreservedContentBlocks != null)
                    {
                        bool firstPreserved = true;
                        for (int p = 0; p < msg.PreservedContentBlocks.Count; p++)
                        {
                            string raw = msg.PreservedContentBlocks[p];
                            if (string.IsNullOrEmpty(raw)) continue;
                            if (!firstPreserved) sb.Append(',');
                            firstPreserved = false;
                            sb.Append(raw);
                            emittedPreserved = true;
                        }
                    }

                    if (!emittedPreserved)
                    {
                        bool firstPart = true;
                        if (!string.IsNullOrEmpty(msg.Text))
                        {
                            firstPart = false;
                            sb.Append("{\"text\":\"");
                            sb.Append(EscapeJsonString(msg.Text));
                            sb.Append("\"}");
                        }
                        for (int t = 0; t < msg.ToolCalls.Count; t++)
                        {
                            if (!firstPart) sb.Append(',');
                            firstPart = false;
                            var tc = msg.ToolCalls[t];
                            sb.Append('{');
                            if (!string.IsNullOrEmpty(tc.ThoughtSignature))
                            {
                                sb.Append("\"thoughtSignature\":\"");
                                sb.Append(EscapeJsonString(tc.ThoughtSignature));
                                sb.Append("\",");
                            }
                            sb.Append("\"functionCall\":{\"name\":\"");
                            sb.Append(EscapeJsonString(tc.FunctionName));
                            sb.Append("\",\"args\":");
                            string args = tc.Arguments;
                            if (string.IsNullOrEmpty(args) || string.IsNullOrEmpty(args.Trim()))
                                sb.Append("{}");
                            else
                                sb.Append(args);
                            sb.Append("}}");
                        }
                    }
                    sb.Append("]}");
                }
                else
                {
                    sb.Append("{\"role\":\"");
                    sb.Append(msg.Sender == MessageSender.User ? "user" : "model");
                    sb.Append("\",\"parts\":[{\"text\":\"");
                    sb.Append(EscapeJsonString(msg.Text));
                    sb.Append("\"}]}");
                }

                i++;
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static void AppendGeminiResponseObject(StringBuilder sb, string raw)
        {
            // Gemini requires functionResponse.response to be a JSON object.
            // Tool results from GameDataTools are already JSON objects, so
            // inline them raw; if a tool ever returns a non-object payload,
            // wrap it under {"content": "..."} so the API still accepts it.
            if (string.IsNullOrEmpty(raw))
            {
                sb.Append("{}");
                return;
            }
            int p = 0;
            while (p < raw.Length && (raw[p] == ' ' || raw[p] == '\t' || raw[p] == '\n' || raw[p] == '\r')) p++;
            if (p < raw.Length && raw[p] == '{')
            {
                sb.Append(raw);
            }
            else
            {
                sb.Append("{\"content\":\"");
                sb.Append(EscapeJsonString(raw));
                sb.Append("\"}");
            }
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

        /// <summary>
        /// Parses a single Anthropic SSE event payload (the JSON after "data: ").
        /// Routes by top-level "type" field:
        ///   content_block_start with content_block.type=tool_use → emits ToolCallId/Name + index
        ///   content_block_delta with delta.type=text_delta        → emits Content
        ///   content_block_delta with delta.type=input_json_delta  → emits ToolCallArguments + index
        /// All other event types (message_start, content_block_stop, message_delta,
        /// message_stop, ping, error) yield an empty StreamDelta. The "index" used
        /// is the Anthropic content block index, which the caller maps to a tool
        /// accumulator slot.
        /// </summary>
        public static StreamDelta ParseAnthropicStreamEvent(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var result = new StreamDelta();

            int rootStart = json.IndexOf('{');
            if (rootStart < 0) return result;
            int rootEnd = FindMatchingClose(json, rootStart);
            if (rootEnd < 0) return result;

            int typeColon = FindKeyAtTopLevel(json, rootStart + 1, rootEnd, "type");
            if (typeColon < 0) return result;
            string eventType = ExtractStringValueAt(json, typeColon, rootEnd);
            if (eventType == null) return result;

            int idxColon = FindKeyAtTopLevel(json, rootStart + 1, rootEnd, "index");
            int blockIndex = idxColon >= 0 ? ExtractIntValueAt(json, idxColon, rootEnd, 0) : 0;

            if (eventType == "content_block_start")
            {
                int cbStart, cbEnd;
                if (!FindObjectValue(json, rootStart + 1, rootEnd, "content_block", out cbStart, out cbEnd))
                    return result;
                int cbTypeColon = FindKeyAtTopLevel(json, cbStart + 1, cbEnd, "type");
                if (cbTypeColon < 0) return result;
                string cbType = ExtractStringValueAt(json, cbTypeColon, cbEnd);

                if (cbType == "tool_use")
                {
                    result.HasToolCalls = true;
                    result.ToolCallIndex = blockIndex;
                    int idColon = FindKeyAtTopLevel(json, cbStart + 1, cbEnd, "id");
                    if (idColon >= 0) result.ToolCallId = ExtractStringValueAt(json, idColon, cbEnd);
                    int nameColon = FindKeyAtTopLevel(json, cbStart + 1, cbEnd, "name");
                    if (nameColon >= 0) result.ToolCallFunctionName = ExtractStringValueAt(json, nameColon, cbEnd);
                    return result;
                }

                if (cbType == "thinking")
                {
                    result.HasPreservedBlock = true;
                    result.PreservedBlockIndex = blockIndex;
                    result.PreservedBlockType = "thinking";
                    int thinkColon = FindKeyAtTopLevel(json, cbStart + 1, cbEnd, "thinking");
                    if (thinkColon >= 0)
                        result.PreservedBlockTextFragment = ExtractStringValueAt(json, thinkColon, cbEnd);
                    int sigColon = FindKeyAtTopLevel(json, cbStart + 1, cbEnd, "signature");
                    if (sigColon >= 0)
                        result.PreservedBlockSignature = ExtractStringValueAt(json, sigColon, cbEnd);
                    return result;
                }

                if (cbType == "redacted_thinking")
                {
                    result.HasPreservedBlock = true;
                    result.PreservedBlockIndex = blockIndex;
                    result.PreservedBlockType = "redacted_thinking";
                    int dataColon = FindKeyAtTopLevel(json, cbStart + 1, cbEnd, "data");
                    if (dataColon >= 0)
                        result.PreservedBlockData = ExtractStringValueAt(json, dataColon, cbEnd);
                    return result;
                }

                return result;
            }

            if (eventType == "content_block_delta")
            {
                int dStart, dEnd;
                if (!FindObjectValue(json, rootStart + 1, rootEnd, "delta", out dStart, out dEnd))
                    return result;
                int dTypeColon = FindKeyAtTopLevel(json, dStart + 1, dEnd, "type");
                if (dTypeColon < 0) return result;
                string dType = ExtractStringValueAt(json, dTypeColon, dEnd);

                if (dType == "text_delta")
                {
                    int textColon = FindKeyAtTopLevel(json, dStart + 1, dEnd, "text");
                    if (textColon >= 0)
                        result.Content = ExtractStringValueAt(json, textColon, dEnd);
                }
                else if (dType == "input_json_delta")
                {
                    int pjColon = FindKeyAtTopLevel(json, dStart + 1, dEnd, "partial_json");
                    if (pjColon >= 0)
                    {
                        result.HasToolCalls = true;
                        result.ToolCallIndex = blockIndex;
                        result.ToolCallArguments = ExtractStringValueAt(json, pjColon, dEnd);
                    }
                }
                else if (dType == "thinking_delta")
                {
                    int textColon = FindKeyAtTopLevel(json, dStart + 1, dEnd, "thinking");
                    if (textColon >= 0)
                    {
                        result.HasPreservedBlock = true;
                        result.PreservedBlockIndex = blockIndex;
                        result.PreservedBlockTextFragment = ExtractStringValueAt(json, textColon, dEnd);
                    }
                }
                else if (dType == "signature_delta")
                {
                    int sigColon = FindKeyAtTopLevel(json, dStart + 1, dEnd, "signature");
                    if (sigColon >= 0)
                    {
                        result.HasPreservedBlock = true;
                        result.PreservedBlockIndex = blockIndex;
                        result.PreservedBlockSignature = ExtractStringValueAt(json, sigColon, dEnd);
                    }
                }
                return result;
            }

            return result;
        }

        /// <summary>
        /// Parses a Gemini :streamGenerateContent SSE event payload.
        /// Iterates candidates[0].content.parts[] and emits one StreamDelta
        /// per part — text parts populate Content; functionCall parts populate
        /// the tool-call fields (Name, raw-JSON Arguments object, synthesized
        /// id, ToolCallIndex = position in the parts array). Gemini emits each
        /// functionCall as a complete block (not fragmented across chunks),
        /// and parallel calls arrive as adjacent parts in one chunk, so per-
        /// chunk part index is sufficient for slot keying.
        /// </summary>
        public static List<StreamDelta> ParseGeminiStreamEvents(string json)
        {
            var results = new List<StreamDelta>();
            if (string.IsNullOrEmpty(json)) return results;

            int rootStart = json.IndexOf('{');
            if (rootStart < 0) return results;
            int rootEnd = FindMatchingClose(json, rootStart);
            if (rootEnd < 0) return results;

            int candArrStart, candArrEnd;
            if (!FindArrayValue(json, rootStart + 1, rootEnd, "candidates", out candArrStart, out candArrEnd))
                return results;

            int firstCand = SkipWhitespace(json, candArrStart + 1);
            if (firstCand >= candArrEnd || json[firstCand] != '{') return results;
            int firstCandEnd = FindMatchingClose(json, firstCand);
            if (firstCandEnd < 0) return results;

            int contentStart, contentEnd;
            if (!FindObjectValue(json, firstCand + 1, firstCandEnd, "content", out contentStart, out contentEnd))
                return results;

            int partsStart, partsEnd;
            if (!FindArrayValue(json, contentStart + 1, contentEnd, "parts", out partsStart, out partsEnd))
                return results;

            int p = SkipWhitespace(json, partsStart + 1);
            int partIndex = 0;
            while (p < partsEnd && json[p] == '{')
            {
                int partEnd = FindMatchingClose(json, p);
                if (partEnd < 0) break;

                int thoughtColon = FindKeyAtTopLevel(json, p + 1, partEnd, "thought");
                bool isThought = thoughtColon >= 0 && ExtractBoolValueAt(json, thoughtColon, partEnd);

                int textColon = FindKeyAtTopLevel(json, p + 1, partEnd, "text");
                // Don't surface thought-summary text in the visible stream;
                // we still preserve the raw part below for round-trip.
                if (textColon >= 0 && !isThought)
                {
                    string text = ExtractStringValueAt(json, textColon, partEnd);
                    if (text != null)
                        results.Add(new StreamDelta { Content = text });
                }

                int fcStart, fcEnd;
                if (FindObjectValue(json, p + 1, partEnd, "functionCall", out fcStart, out fcEnd))
                {
                    var d = new StreamDelta { HasToolCalls = true, ToolCallIndex = partIndex };

                    int nameColon = FindKeyAtTopLevel(json, fcStart + 1, fcEnd, "name");
                    if (nameColon >= 0)
                        d.ToolCallFunctionName = ExtractStringValueAt(json, nameColon, fcEnd);

                    int argsObjStart, argsObjEnd;
                    if (FindObjectValue(json, fcStart + 1, fcEnd, "args", out argsObjStart, out argsObjEnd))
                        d.ToolCallArguments = json.Substring(argsObjStart, argsObjEnd - argsObjStart + 1);
                    else
                        d.ToolCallArguments = "{}";

                    int sigColon = FindKeyAtTopLevel(json, p + 1, partEnd, "thoughtSignature");
                    if (sigColon >= 0)
                        d.ToolCallThoughtSignature = ExtractStringValueAt(json, sigColon, partEnd);

                    // Gemini doesn't return a call id; synthesize one so the
                    // existing accumulator/dispatch path stays unchanged.
                    d.ToolCallId = "gemini_call_" + partIndex + "_" + (d.ToolCallFunctionName ?? "fn");
                    results.Add(d);
                }

                // Preserve the full part verbatim so the assistant turn can be
                // echoed back unchanged. Required for thinking-enabled models —
                // Gemini 400s if any thought_signature (on thought-text or
                // functionCall parts) is stripped from the next request.
                results.Add(new StreamDelta
                {
                    PreservedRawJson = json.Substring(p, partEnd - p + 1)
                });

                p = SkipWhitespace(json, partEnd + 1);
                if (p < partsEnd && json[p] == ',') p = SkipWhitespace(json, p + 1);
                partIndex++;
            }

            return results;
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

        private static bool ExtractBoolValueAt(string s, int colonPos, int end)
        {
            int p = SkipWhitespace(s, colonPos + 1);
            return p + 4 <= end && string.CompareOrdinal(s, p, "true", 0, 4) == 0;
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
