using System.Collections.Generic;
using NUnit.Framework;

namespace Kerpilot.Tests
{
    [TestFixture]
    public class ToolAvailabilityTests
    {
        // ── ExecuteTool dispatch ──

        [Test]
        public void ExecuteTool_UnknownTool_ReturnsError()
        {
            string result = ToolDefinitions.ExecuteTool("nonexistent_tool", "{}");
            Assert.That(result, Does.Contain("\"error\""));
            Assert.That(result, Does.Contain("Unknown tool"));
            Assert.That(result, Does.Contain("nonexistent_tool"));
        }

        [Test]
        public void ExecuteTool_UnknownTool_EscapesName()
        {
            // Tool name with special chars should be escaped in the error JSON
            string result = ToolDefinitions.ExecuteTool("bad\"tool", "{}");
            Assert.That(result, Does.Contain("\\\""));
        }

        [Test]
        public void ExecuteTool_GetPartInfo_MissingParam_ReturnsError()
        {
            // When called with empty arguments, get_part_info should return an error
            // (not crash) because part_name is required
            string result = ToolDefinitions.ExecuteTool("get_part_info", "{}");
            Assert.That(result, Does.Contain("\"error\""));
        }

        [Test]
        public void ExecuteTool_GetCelestialBody_MissingParam_ReturnsError()
        {
            string result = ToolDefinitions.ExecuteTool("get_celestial_body", "{}");
            Assert.That(result, Does.Contain("\"error\""));
        }

        [Test]
        public void ExecuteTool_SearchAvailableParts_InvalidCategory_ReturnsError()
        {
            string result = ToolDefinitions.ExecuteTool("search_available_parts", "{\"category\":\"NotACategory\"}");
            Assert.That(result, Does.Contain("\"error\""));
            Assert.That(result, Does.Contain("Unknown category"));
        }

        // ── JsonHelper parsing ──

        [Test]
        public void ExtractJsonStringValue_PartName()
        {
            string json = "{\"part_name\":\"FL-T400 Fuel Tank\"}";
            string value = JsonHelper.ExtractJsonStringValue(json, "part_name");
            Assert.That(value, Is.EqualTo("FL-T400 Fuel Tank"));
        }

        [Test]
        public void ExtractJsonStringValue_BodyName()
        {
            string json = "{\"body_name\":\"Kerbin\"}";
            string value = JsonHelper.ExtractJsonStringValue(json, "body_name");
            Assert.That(value, Is.EqualTo("Kerbin"));
        }

        [Test]
        public void ExtractJsonStringValue_Query()
        {
            string json = "{\"query\":\"Hohmann transfer orbit\"}";
            string value = JsonHelper.ExtractJsonStringValue(json, "query");
            Assert.That(value, Is.EqualTo("Hohmann transfer orbit"));
        }

        [Test]
        public void ExtractJsonStringValue_MissingKey_ReturnsNull()
        {
            string json = "{\"other_key\":\"value\"}";
            Assert.That(JsonHelper.ExtractJsonStringValue(json, "part_name"), Is.Null);
        }

        [Test]
        public void ExtractJsonStringValue_NullValue_ReturnsNull()
        {
            string json = "{\"part_name\":null}";
            Assert.That(JsonHelper.ExtractJsonStringValue(json, "part_name"), Is.Null);
        }

        [Test]
        public void ExtractJsonStringValue_EscapedChars()
        {
            string json = "{\"query\":\"test\\\"quoted\\\" value\"}";
            string value = JsonHelper.ExtractJsonStringValue(json, "query");
            Assert.That(value, Is.EqualTo("test\"quoted\" value"));
        }

        [Test]
        public void ExtractJsonStringValue_EmptyJson_ReturnsNull()
        {
            Assert.That(JsonHelper.ExtractJsonStringValue("", "key"), Is.Null);
            Assert.That(JsonHelper.ExtractJsonStringValue(null, "key"), Is.Null);
        }

        // ── ParseStreamDelta ──

        [Test]
        public void ParseStreamDelta_ContentChunk_ExtractsContent()
        {
            string json = "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hello\"}}]}";
            var delta = JsonHelper.ParseStreamDelta(json);
            Assert.That(delta, Is.Not.Null);
            Assert.That(delta.Content, Is.EqualTo("Hello"));
            Assert.That(delta.HasToolCalls, Is.False);
        }

        [Test]
        public void ParseStreamDelta_ToolCallChunk_PopulatesToolFields()
        {
            string json = "{\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":2,\"id\":\"call_abc123\",\"function\":{\"name\":\"get_vessel_parts\",\"arguments\":\"{\\\"part\"}}]}}]}";
            var delta = JsonHelper.ParseStreamDelta(json);
            Assert.That(delta.HasToolCalls, Is.True);
            Assert.That(delta.ToolCallIndex, Is.EqualTo(2));
            Assert.That(delta.ToolCallId, Is.EqualTo("call_abc123"));
            Assert.That(delta.ToolCallFunctionName, Is.EqualTo("get_vessel_parts"));
            Assert.That(delta.ToolCallArguments, Is.EqualTo("{\"part"));
        }

        [Test]
        public void ParseStreamDelta_ToolCallsNull_NotDetected()
        {
            string json = "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"hi\",\"tool_calls\":null}}]}";
            var delta = JsonHelper.ParseStreamDelta(json);
            Assert.That(delta.HasToolCalls, Is.False);
            Assert.That(delta.Content, Is.EqualTo("hi"));
        }

        [Test]
        public void ParseStreamDelta_OpenRouterUsageMetadata_NoContentNoToolCalls()
        {
            // OpenRouter sends a final chunk with usage info and empty choices
            string json = "{\"id\":\"gen-xyz\",\"provider\":\"Google\",\"model\":\"google/gemini-2.5-pro\",\"object\":\"chat.completion.chunk\",\"created\":1730000000,\"choices\":[],\"usage\":{\"prompt_tokens\":100,\"completion_tokens\":50,\"total_tokens\":150,\"cost\":0.00123}}";
            var delta = JsonHelper.ParseStreamDelta(json);
            Assert.That(delta, Is.Not.Null);
            Assert.That(delta.Content, Is.Null);
            Assert.That(delta.HasToolCalls, Is.False);
        }

        [Test]
        public void ParseStreamDelta_GeminiFinishChunk_NoContent()
        {
            // Some providers send a finish chunk with empty delta plus usage
            string json = "{\"choices\":[{\"finish_reason\":\"stop\",\"index\":0,\"delta\":{}}],\"usage\":{\"prompt_tokens\":50,\"completion_tokens\":20,\"total_tokens\":70}}";
            var delta = JsonHelper.ParseStreamDelta(json);
            Assert.That(delta.Content, Is.Null);
            Assert.That(delta.HasToolCalls, Is.False);
        }

        [Test]
        public void ParseStreamDelta_MetadataChunkWithToolCallsKeyword_NotMisdetected()
        {
            // A metadata chunk that mentions "tool_calls" outside choices[0].delta
            // (e.g. as part of a provider's request echo) must not trigger tool-call parsing
            string json = "{\"id\":\"gen-1\",\"x_request\":{\"tool_calls\":\"enabled\"},\"choices\":[]}";
            var delta = JsonHelper.ParseStreamDelta(json);
            Assert.That(delta.HasToolCalls, Is.False);
        }

        [Test]
        public void ParseStreamDelta_InvalidJson_ReturnsNull()
        {
            Assert.That(JsonHelper.ParseStreamDelta("not json"), Is.Null);
            Assert.That(JsonHelper.ParseStreamDelta(null), Is.Null);
            Assert.That(JsonHelper.ParseStreamDelta(""), Is.Null);
        }

        // ── ChatMessage tool call model ──

        [Test]
        public void ToolCall_StoresProperties()
        {
            var tc = new ToolCall("call_123", "get_vessel_parts", "{}");
            Assert.That(tc.Id, Is.EqualTo("call_123"));
            Assert.That(tc.FunctionName, Is.EqualTo("get_vessel_parts"));
            Assert.That(tc.Arguments, Is.EqualTo("{}"));
        }

        [Test]
        public void ChatMessage_CreateAssistantToolCall_SetsRole()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall("call_1", "get_vessel_parts", "{}")
            };
            var msg = ChatMessage.CreateAssistantToolCall(toolCalls);
            Assert.That(msg.Role, Is.EqualTo(MessageRole.Assistant));
            Assert.That(msg.ToolCalls, Is.Not.Null);
            Assert.That(msg.ToolCalls.Count, Is.EqualTo(1));
            Assert.That(msg.ToolCalls[0].FunctionName, Is.EqualTo("get_vessel_parts"));
        }

        [Test]
        public void ChatMessage_CreateToolResult_SetsRole()
        {
            var msg = ChatMessage.CreateToolResult("call_1", "{\"parts\":[]}");
            Assert.That(msg.Role, Is.EqualTo(MessageRole.Tool));
            Assert.That(msg.ToolCallId, Is.EqualTo("call_1"));
            Assert.That(msg.Text, Is.EqualTo("{\"parts\":[]}"));
        }

        // ── BuildChatRequestBody ──

        [Test]
        public void GetToolsJsonArray_ContainsSearchAvailableParts()
        {
            string json = ToolDefinitions.GetToolsJsonArray();
            Assert.That(json, Does.Contain("search_available_parts"));
            Assert.That(json, Does.Contain("\"category\""));
            Assert.That(json, Does.Contain("\"search\""));
        }

        [Test]
        public void GetToolStatusLabel_SearchAvailableParts_ReturnsLabel()
        {
            string label = ToolDefinitions.GetToolStatusLabel("search_available_parts");
            Assert.That(label, Does.Contain("..."));
            Assert.That(label, Is.Not.EqualTo("Looking up game data..."));
        }

        [Test]
        public void BuildChatRequestBody_IncludesToolsJson()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "What parts do I have?")
            };
            string toolsJson = ToolDefinitions.GetToolsJsonArray();
            string body = JsonHelper.BuildChatRequestBody(history, "gpt-4", "You are helpful.", toolsJson);

            Assert.That(body, Does.Contain("\"tools\":"));
            Assert.That(body, Does.Contain("get_vessel_parts"));
        }

        [Test]
        public void BuildChatRequestBody_NullTools_OmitsToolsField()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "Hello")
            };
            string body = JsonHelper.BuildChatRequestBody(history, "gpt-4", "You are helpful.", null);
            Assert.That(body, Does.Not.Contain("\"tools\""));
        }

        [Test]
        public void BuildChatRequestBody_ToolCallMessage_IncludesToolCallsArray()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall("call_abc", "get_vessel_parts", "{}")
            };
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "Show my parts"),
                ChatMessage.CreateAssistantToolCall(toolCalls),
                ChatMessage.CreateToolResult("call_abc", "{\"vessel_name\":\"Test\",\"parts\":[]}")
            };
            string body = JsonHelper.BuildChatRequestBody(history, "gpt-4", "System", ToolDefinitions.GetToolsJsonArray());

            Assert.That(body, Does.Contain("\"tool_calls\":["));
            Assert.That(body, Does.Contain("\"id\":\"call_abc\""));
            Assert.That(body, Does.Contain("\"role\":\"tool\""));
            Assert.That(body, Does.Contain("\"tool_call_id\":\"call_abc\""));
        }
    }
}
