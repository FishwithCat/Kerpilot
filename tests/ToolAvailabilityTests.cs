using System.Collections.Generic;
using NUnit.Framework;

namespace Kerpilot.Tests
{
    [TestFixture]
    public class ToolAvailabilityTests
    {
        // All tool names that should be registered
        private static readonly string[] AllToolNames =
        {
            "get_vessel_parts",
            "get_part_info",
            "get_celestial_body",
            "get_active_contracts",
            "get_vessel_delta_v",
            "get_vessel_orbit",
            "get_vessel_status",
            "get_atmosphere_data"
        };

        private string toolsJson;

        [SetUp]
        public void SetUp()
        {
            toolsJson = ToolDefinitions.GetToolsJsonArray();
        }

        // ── Tool definitions JSON structure ──

        [Test]
        public void GetToolsJsonArray_ReturnsNonEmpty()
        {
            Assert.That(toolsJson, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void GetToolsJsonArray_IsJsonArray()
        {
            Assert.That(toolsJson, Does.StartWith("["));
            Assert.That(toolsJson, Does.EndWith("]"));
        }

        [Test]
        [TestCaseSource(nameof(AllToolNames))]
        public void GetToolsJsonArray_ContainsTool(string toolName)
        {
            // Each tool should appear as "name":"<toolName>" in the JSON
            string needle = "\"name\":\"" + toolName + "\"";
            Assert.That(toolsJson, Does.Contain(needle),
                $"Tool '{toolName}' not found in tools JSON array");
        }

        [Test]
        public void GetToolsJsonArray_ContainsAllNineTools()
        {
            int count = 0;
            int pos = 0;
            while (true)
            {
                int idx = toolsJson.IndexOf("\"type\":\"function\"", pos);
                if (idx < 0) break;
                count++;
                pos = idx + 1;
            }
            Assert.That(count, Is.EqualTo(8), "Expected exactly 8 tool definitions");
        }

        [Test]
        [TestCaseSource(nameof(AllToolNames))]
        public void GetToolsJsonArray_EachToolHasDescription(string toolName)
        {
            // Find the tool section and verify it has a description field
            int nameIdx = toolsJson.IndexOf("\"name\":\"" + toolName + "\"");
            Assert.That(nameIdx, Is.GreaterThanOrEqualTo(0));

            // Look for "description" near this tool (within 500 chars before/after)
            int searchStart = System.Math.Max(0, nameIdx - 300);
            int searchEnd = System.Math.Min(toolsJson.Length, nameIdx + 500);
            string vicinity = toolsJson.Substring(searchStart, searchEnd - searchStart);
            Assert.That(vicinity, Does.Contain("\"description\":\""),
                $"Tool '{toolName}' missing description");
        }

        [Test]
        [TestCaseSource(nameof(AllToolNames))]
        public void GetToolsJsonArray_EachToolHasParameters(string toolName)
        {
            int nameIdx = toolsJson.IndexOf("\"name\":\"" + toolName + "\"");
            int searchStart = System.Math.Max(0, nameIdx - 100);
            int searchEnd = System.Math.Min(toolsJson.Length, nameIdx + 500);
            string vicinity = toolsJson.Substring(searchStart, searchEnd - searchStart);
            Assert.That(vicinity, Does.Contain("\"parameters\":{"),
                $"Tool '{toolName}' missing parameters schema");
        }

        // ── Tool requiring specific parameters ──

        [Test]
        public void GetPartInfo_RequiresPartName()
        {
            Assert.That(toolsJson, Does.Contain("\"part_name\""));
            // Check it's in the required array
            int partInfoIdx = toolsJson.IndexOf("\"name\":\"get_part_info\"");
            int requiredIdx = toolsJson.IndexOf("\"required\":[\"part_name\"]", partInfoIdx);
            Assert.That(requiredIdx, Is.GreaterThan(partInfoIdx),
                "get_part_info should require part_name parameter");
        }

        [Test]
        public void GetCelestialBody_RequiresBodyName()
        {
            int cbIdx = toolsJson.IndexOf("\"name\":\"get_celestial_body\"");
            int requiredIdx = toolsJson.IndexOf("\"required\":[\"body_name\"]", cbIdx);
            Assert.That(requiredIdx, Is.GreaterThan(cbIdx),
                "get_celestial_body should require body_name parameter");
        }

        [Test]
        public void GetAtmosphereData_BodyNameIsOptional()
        {
            int atmIdx = toolsJson.IndexOf("\"name\":\"get_atmosphere_data\"");
            int nextToolIdx = toolsJson.IndexOf("\"name\":\"", atmIdx + 1);
            string atmSection = nextToolIdx > 0
                ? toolsJson.Substring(atmIdx, nextToolIdx - atmIdx)
                : toolsJson.Substring(atmIdx);
            Assert.That(atmSection, Does.Contain("\"required\":[]"),
                "get_atmosphere_data should have no required parameters");
        }

        // ── Status labels ──

        [Test]
        [TestCaseSource(nameof(AllToolNames))]
        public void GetToolStatusLabel_ReturnsNonEmptyForAllTools(string toolName)
        {
            string label = ToolDefinitions.GetToolStatusLabel(toolName);
            Assert.That(label, Is.Not.Null.And.Not.Empty,
                $"Status label for '{toolName}' should not be empty");
        }

        [Test]
        [TestCaseSource(nameof(AllToolNames))]
        public void GetToolStatusLabel_EndsWithEllipsis(string toolName)
        {
            string label = ToolDefinitions.GetToolStatusLabel(toolName);
            Assert.That(label, Does.EndWith("..."),
                $"Status label for '{toolName}' should end with '...'");
        }

        [Test]
        public void GetToolStatusLabel_UnknownToolReturnsFallback()
        {
            string label = ToolDefinitions.GetToolStatusLabel("nonexistent_tool");
            Assert.That(label, Is.Not.Null.And.Not.Empty);
            Assert.That(label, Does.EndWith("..."));
        }

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

        // ── JsonHelper tool-related parsing ──

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

        // ── HasToolCalls detection ──

        [Test]
        public void HasToolCalls_WithArray_ReturnsTrue()
        {
            string json = "{\"delta\":{\"tool_calls\":[{\"index\":0}]}}";
            Assert.That(JsonHelper.HasToolCalls(json), Is.True);
        }

        [Test]
        public void HasToolCalls_WithNull_ReturnsFalse()
        {
            string json = "{\"delta\":{\"tool_calls\":null}}";
            Assert.That(JsonHelper.HasToolCalls(json), Is.False);
        }

        [Test]
        public void HasToolCalls_NoField_ReturnsFalse()
        {
            string json = "{\"delta\":{\"content\":\"hello\"}}";
            Assert.That(JsonHelper.HasToolCalls(json), Is.False);
        }

        [Test]
        public void HasToolCalls_NullInput_ReturnsFalse()
        {
            Assert.That(JsonHelper.HasToolCalls(null), Is.False);
        }

        // ── Tool call SSE parsing ──

        [Test]
        public void ExtractToolCallIndex_ReturnsIndex()
        {
            string json = "{\"delta\":{\"tool_calls\":[{\"index\":2,\"id\":\"call_abc\"}]}}";
            Assert.That(JsonHelper.ExtractToolCallIndex(json), Is.EqualTo(2));
        }

        [Test]
        public void ExtractToolCallId_ReturnsId()
        {
            string json = "{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call_abc123\"}]}}";
            Assert.That(JsonHelper.ExtractToolCallId(json), Is.EqualTo("call_abc123"));
        }

        [Test]
        public void ExtractToolCallFunctionName_ReturnsName()
        {
            string json = "{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"name\":\"get_vessel_parts\",\"arguments\":\"\"}}]}}";
            Assert.That(JsonHelper.ExtractToolCallFunctionName(json), Is.EqualTo("get_vessel_parts"));
        }

        [Test]
        public void ExtractToolCallArguments_ReturnsFragment()
        {
            string json = "{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"name\":\"get_part_info\",\"arguments\":\"{\\\"part\"}}]}}";
            Assert.That(JsonHelper.ExtractToolCallArguments(json), Is.EqualTo("{\"part"));
        }

        [Test]
        public void ExtractToolCallFunctionName_NoToolCalls_ReturnsNull()
        {
            string json = "{\"delta\":{\"content\":\"hello\"}}";
            Assert.That(JsonHelper.ExtractToolCallFunctionName(json), Is.Null);
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

        // ── BuildChatRequestBody includes tools ──

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
