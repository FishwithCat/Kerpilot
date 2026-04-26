using System.Collections.Generic;
using NUnit.Framework;

namespace Kerpilot.Tests
{
    [TestFixture]
    public class AnthropicProviderTests
    {
        // ── ChatProviderDetector ──

        [Test]
        public void Detect_AnthropicHost_ReturnsAnthropic()
        {
            Assert.That(ChatProviderDetector.Detect("https://api.anthropic.com"),
                Is.EqualTo(ChatProvider.Anthropic));
            Assert.That(ChatProviderDetector.Detect("https://api.anthropic.com/v1"),
                Is.EqualTo(ChatProvider.Anthropic));
        }

        [Test]
        public void Detect_AnthropicProxyPath_ReturnsAnthropic()
        {
            Assert.That(ChatProviderDetector.Detect("https://api.deepseek.com/anthropic"),
                Is.EqualTo(ChatProvider.Anthropic));
            Assert.That(ChatProviderDetector.Detect("https://api.deepseek.com/anthropic/"),
                Is.EqualTo(ChatProvider.Anthropic));
        }

        [Test]
        public void Detect_OpenAIHost_ReturnsOpenAI()
        {
            Assert.That(ChatProviderDetector.Detect("https://api.openai.com/v1"),
                Is.EqualTo(ChatProvider.OpenAICompatible));
            Assert.That(ChatProviderDetector.Detect("https://openrouter.ai/api/v1"),
                Is.EqualTo(ChatProvider.OpenAICompatible));
            Assert.That(ChatProviderDetector.Detect("https://generativelanguage.googleapis.com/v1beta/openai"),
                Is.EqualTo(ChatProvider.OpenAICompatible));
            Assert.That(ChatProviderDetector.Detect("https://api.deepseek.com/v1"),
                Is.EqualTo(ChatProvider.OpenAICompatible));
        }

        [Test]
        public void Detect_NullOrEmpty_ReturnsOpenAI()
        {
            Assert.That(ChatProviderDetector.Detect(null), Is.EqualTo(ChatProvider.OpenAICompatible));
            Assert.That(ChatProviderDetector.Detect(""), Is.EqualTo(ChatProvider.OpenAICompatible));
        }

        // ── BuildAnthropicUrl ──

        [Test]
        public void BuildAnthropicUrl_BareHost_AppendsV1Messages()
        {
            Assert.That(LlmClient.BuildAnthropicUrl("https://api.anthropic.com"),
                Is.EqualTo("https://api.anthropic.com/v1/messages"));
        }

        [Test]
        public void BuildAnthropicUrl_HostWithV1_AppendsMessages()
        {
            Assert.That(LlmClient.BuildAnthropicUrl("https://api.anthropic.com/v1"),
                Is.EqualTo("https://api.anthropic.com/v1/messages"));
            Assert.That(LlmClient.BuildAnthropicUrl("https://api.anthropic.com/v1/"),
                Is.EqualTo("https://api.anthropic.com/v1/messages"));
        }

        [Test]
        public void BuildAnthropicUrl_AnthropicProxyPath_AppendsV1Messages()
        {
            Assert.That(LlmClient.BuildAnthropicUrl("https://api.deepseek.com/anthropic"),
                Is.EqualTo("https://api.deepseek.com/anthropic/v1/messages"));
        }

        // ── GetToolsJsonArrayAnthropic ──

        [Test]
        public void GetToolsJsonArrayAnthropic_UsesInputSchema_NotParameters()
        {
            string json = ToolDefinitions.GetToolsJsonArrayAnthropic();
            Assert.That(json, Does.Contain("\"input_schema\""));
            Assert.That(json, Does.Not.Contain("\"parameters\""));
            Assert.That(json, Does.Not.Contain("\"type\":\"function\""));
        }

        [Test]
        public void GetToolsJsonArrayAnthropic_ContainsAllToolNames()
        {
            string json = ToolDefinitions.GetToolsJsonArrayAnthropic();
            Assert.That(json, Does.Contain("get_vessel_parts"));
            Assert.That(json, Does.Contain("get_vessel_delta_v"));
            Assert.That(json, Does.Contain("search_available_parts"));
            Assert.That(json, Does.Contain("analyze_vessel"));
        }

        [Test]
        public void GetToolsJsonArrayOpenAI_StillUsesParameters()
        {
            // Sanity: OpenAI format must still wrap with type=function and use "parameters"
            string json = ToolDefinitions.GetToolsJsonArray();
            Assert.That(json, Does.Contain("\"type\":\"function\""));
            Assert.That(json, Does.Contain("\"parameters\""));
            Assert.That(json, Does.Not.Contain("\"input_schema\""));
        }

        // ── BuildAnthropicRequestBody ──

        [Test]
        public void BuildAnthropicRequestBody_PutsSystemAtTopLevel()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "hi")
            };
            string body = JsonHelper.BuildAnthropicRequestBody(
                history, "claude-3-5-sonnet-latest", "You are helpful.", null, 4096);

            Assert.That(body, Does.Contain("\"system\":\"You are helpful.\""));
            Assert.That(body, Does.Contain("\"max_tokens\":4096"));
            Assert.That(body, Does.Contain("\"stream\":true"));
            Assert.That(body, Does.Contain("\"role\":\"user\""));
            Assert.That(body, Does.Not.Contain("\"role\":\"system\""));
        }

        [Test]
        public void BuildAnthropicRequestBody_IncludesTools()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "show parts")
            };
            string body = JsonHelper.BuildAnthropicRequestBody(
                history, "claude-3-5-sonnet-latest", "sys",
                ToolDefinitions.GetToolsJsonArrayAnthropic(), 4096);

            Assert.That(body, Does.Contain("\"tools\":"));
            Assert.That(body, Does.Contain("\"input_schema\""));
            Assert.That(body, Does.Contain("get_vessel_parts"));
        }

        [Test]
        public void BuildAnthropicRequestBody_AssistantToolCall_EmitsToolUseBlock()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall("toolu_abc", "get_vessel_parts", "{\"foo\":1}")
            };
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "show parts"),
                ChatMessage.CreateAssistantToolCall(toolCalls),
                ChatMessage.CreateToolResult("toolu_abc", "{\"parts\":[]}")
            };
            string body = JsonHelper.BuildAnthropicRequestBody(
                history, "claude-3-5-sonnet-latest", "sys", null, 4096);

            Assert.That(body, Does.Contain("\"type\":\"tool_use\""));
            Assert.That(body, Does.Contain("\"id\":\"toolu_abc\""));
            Assert.That(body, Does.Contain("\"name\":\"get_vessel_parts\""));
            // input must be inlined as raw JSON object (not stringified)
            Assert.That(body, Does.Contain("\"input\":{\"foo\":1}"));
            // tool result is wrapped in a user message with content blocks
            Assert.That(body, Does.Contain("\"type\":\"tool_result\""));
            Assert.That(body, Does.Contain("\"tool_use_id\":\"toolu_abc\""));
        }

        [Test]
        public void BuildAnthropicRequestBody_EmptyArguments_DefaultsToEmptyObject()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall("toolu_x", "get_contracts", "")
            };
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "hi"),
                ChatMessage.CreateAssistantToolCall(toolCalls)
            };
            string body = JsonHelper.BuildAnthropicRequestBody(
                history, "claude-3-5-sonnet-latest", "sys", null, 4096);

            Assert.That(body, Does.Contain("\"input\":{}"));
        }

        [Test]
        public void BuildAnthropicRequestBody_ConsecutiveToolResults_GroupedInOneUserMessage()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall("toolu_a", "get_vessel_parts", "{}"),
                new ToolCall("toolu_b", "get_contracts", "{}")
            };
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "go"),
                ChatMessage.CreateAssistantToolCall(toolCalls),
                ChatMessage.CreateToolResult("toolu_a", "A"),
                ChatMessage.CreateToolResult("toolu_b", "B")
            };
            string body = JsonHelper.BuildAnthropicRequestBody(
                history, "claude-3-5-sonnet-latest", "sys", null, 4096);

            // Expect both tool_result blocks inside a single user-role message
            int firstToolResult = body.IndexOf("\"tool_use_id\":\"toolu_a\"");
            int secondToolResult = body.IndexOf("\"tool_use_id\":\"toolu_b\"");
            int userBetween = body.IndexOf("\"role\":\"user\"", firstToolResult + 1);
            Assert.That(firstToolResult, Is.GreaterThan(0));
            Assert.That(secondToolResult, Is.GreaterThan(firstToolResult));
            // No user-role boundary should appear between the two results
            Assert.That(userBetween, Is.EqualTo(-1).Or.GreaterThan(secondToolResult));
        }

        // ── ParseAnthropicStreamEvent ──

        [Test]
        public void ParseAnthropicStreamEvent_TextDelta_ExtractsContent()
        {
            string json = "{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello\"}}";
            var delta = JsonHelper.ParseAnthropicStreamEvent(json);
            Assert.That(delta, Is.Not.Null);
            Assert.That(delta.Content, Is.EqualTo("Hello"));
            Assert.That(delta.HasToolCalls, Is.False);
        }

        [Test]
        public void ParseAnthropicStreamEvent_ToolUseBlockStart_PopulatesIdAndName()
        {
            string json = "{\"type\":\"content_block_start\",\"index\":1,\"content_block\":{\"type\":\"tool_use\",\"id\":\"toolu_X\",\"name\":\"get_vessel_parts\",\"input\":{}}}";
            var delta = JsonHelper.ParseAnthropicStreamEvent(json);
            Assert.That(delta.HasToolCalls, Is.True);
            Assert.That(delta.ToolCallIndex, Is.EqualTo(1));
            Assert.That(delta.ToolCallId, Is.EqualTo("toolu_X"));
            Assert.That(delta.ToolCallFunctionName, Is.EqualTo("get_vessel_parts"));
        }

        [Test]
        public void ParseAnthropicStreamEvent_TextBlockStart_NotMisdetectedAsToolCall()
        {
            string json = "{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}";
            var delta = JsonHelper.ParseAnthropicStreamEvent(json);
            Assert.That(delta.HasToolCalls, Is.False);
            Assert.That(delta.Content, Is.Null);
        }

        [Test]
        public void ParseAnthropicStreamEvent_InputJsonDelta_AccumulatesArguments()
        {
            string json = "{\"type\":\"content_block_delta\",\"index\":1,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"{\\\"part\"}}";
            var delta = JsonHelper.ParseAnthropicStreamEvent(json);
            Assert.That(delta.HasToolCalls, Is.True);
            Assert.That(delta.ToolCallIndex, Is.EqualTo(1));
            Assert.That(delta.ToolCallArguments, Is.EqualTo("{\"part"));
        }

        [Test]
        public void ParseAnthropicStreamEvent_MessageStart_NoOp()
        {
            string json = "{\"type\":\"message_start\",\"message\":{\"id\":\"msg_X\",\"role\":\"assistant\"}}";
            var delta = JsonHelper.ParseAnthropicStreamEvent(json);
            Assert.That(delta.Content, Is.Null);
            Assert.That(delta.HasToolCalls, Is.False);
        }

        [Test]
        public void ParseAnthropicStreamEvent_MessageStop_NoOp()
        {
            string json = "{\"type\":\"message_stop\"}";
            var delta = JsonHelper.ParseAnthropicStreamEvent(json);
            Assert.That(delta.Content, Is.Null);
            Assert.That(delta.HasToolCalls, Is.False);
        }

        [Test]
        public void ParseAnthropicStreamEvent_PingEvent_NoOp()
        {
            string json = "{\"type\":\"ping\"}";
            var delta = JsonHelper.ParseAnthropicStreamEvent(json);
            Assert.That(delta.Content, Is.Null);
            Assert.That(delta.HasToolCalls, Is.False);
        }

        [Test]
        public void ParseAnthropicStreamEvent_NullOrEmpty_ReturnsNull()
        {
            Assert.That(JsonHelper.ParseAnthropicStreamEvent(null), Is.Null);
            Assert.That(JsonHelper.ParseAnthropicStreamEvent(""), Is.Null);
        }

        // ── Extended thinking blocks ──

        [Test]
        public void ParseAnthropicStreamEvent_ThinkingBlockStart_DetectsType()
        {
            string json = "{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"thinking\",\"thinking\":\"\"}}";
            var delta = JsonHelper.ParseAnthropicStreamEvent(json);
            Assert.That(delta.HasPreservedBlock, Is.True);
            Assert.That(delta.PreservedBlockType, Is.EqualTo("thinking"));
            Assert.That(delta.PreservedBlockIndex, Is.EqualTo(0));
            Assert.That(delta.HasToolCalls, Is.False);
            Assert.That(delta.Content, Is.Null);
        }

        [Test]
        public void ParseAnthropicStreamEvent_ThinkingDelta_AccumulatesText()
        {
            string json = "{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"thinking_delta\",\"thinking\":\"reasoning step\"}}";
            var delta = JsonHelper.ParseAnthropicStreamEvent(json);
            Assert.That(delta.HasPreservedBlock, Is.True);
            Assert.That(delta.PreservedBlockIndex, Is.EqualTo(0));
            Assert.That(delta.PreservedBlockTextFragment, Is.EqualTo("reasoning step"));
        }

        [Test]
        public void ParseAnthropicStreamEvent_SignatureDelta_CapturesSignature()
        {
            string json = "{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"signature_delta\",\"signature\":\"abc123signature\"}}";
            var delta = JsonHelper.ParseAnthropicStreamEvent(json);
            Assert.That(delta.HasPreservedBlock, Is.True);
            Assert.That(delta.PreservedBlockSignature, Is.EqualTo("abc123signature"));
        }

        [Test]
        public void ParseAnthropicStreamEvent_RedactedThinkingStart_CapturesData()
        {
            string json = "{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"redacted_thinking\",\"data\":\"opaque-blob\"}}";
            var delta = JsonHelper.ParseAnthropicStreamEvent(json);
            Assert.That(delta.HasPreservedBlock, Is.True);
            Assert.That(delta.PreservedBlockType, Is.EqualTo("redacted_thinking"));
            Assert.That(delta.PreservedBlockData, Is.EqualTo("opaque-blob"));
        }

        [Test]
        public void BuildAnthropicRequestBody_PreservedBlocks_EmittedBeforeTextAndToolUse()
        {
            var preserved = new List<string>
            {
                "{\"type\":\"thinking\",\"thinking\":\"step 1\",\"signature\":\"sig\"}"
            };
            var toolCalls = new List<ToolCall>
            {
                new ToolCall("toolu_a", "get_vessel_parts", "{}")
            };
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "show parts"),
                ChatMessage.CreateAssistantToolCall(toolCalls, "ok", preserved)
            };
            string body = JsonHelper.BuildAnthropicRequestBody(
                history, "claude-3-7-sonnet-latest", "sys", null, 4096);

            int thinkingPos = body.IndexOf("\"type\":\"thinking\"");
            int textPos = body.IndexOf("\"type\":\"text\"");
            int toolUsePos = body.IndexOf("\"type\":\"tool_use\"");
            Assert.That(thinkingPos, Is.GreaterThan(0), "thinking block missing");
            Assert.That(textPos, Is.GreaterThan(thinkingPos), "thinking must come before text");
            Assert.That(toolUsePos, Is.GreaterThan(textPos), "tool_use must come after text");
            // signature must be preserved verbatim
            Assert.That(body, Does.Contain("\"signature\":\"sig\""));
        }

        [Test]
        public void BuildAnthropicRequestBody_PreservedBlocksOnly_NoTextNoOtherBlocks_StillEmits()
        {
            var preserved = new List<string>
            {
                "{\"type\":\"thinking\",\"thinking\":\"x\",\"signature\":\"s\"}"
            };
            var toolCalls = new List<ToolCall>
            {
                new ToolCall("toolu_a", "get_vessel_parts", "{}")
            };
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "go"),
                ChatMessage.CreateAssistantToolCall(toolCalls, null, preserved)
            };
            string body = JsonHelper.BuildAnthropicRequestBody(
                history, "claude-3-7-sonnet-latest", "sys", null, 4096);

            // Sanity: no malformed leading comma in the content array
            Assert.That(body, Does.Not.Contain("\"content\":[,"));
            Assert.That(body, Does.Contain("\"type\":\"thinking\""));
            Assert.That(body, Does.Contain("\"type\":\"tool_use\""));
        }

        [Test]
        public void ChatMessage_CreateAssistantToolCall_StoresPreservedBlocks()
        {
            var preserved = new List<string> { "{\"type\":\"thinking\",\"thinking\":\"\",\"signature\":\"\"}" };
            var toolCalls = new List<ToolCall> { new ToolCall("id", "fn", "{}") };
            var msg = ChatMessage.CreateAssistantToolCall(toolCalls, null, preserved);

            Assert.That(msg.PreservedContentBlocks, Is.Not.Null);
            Assert.That(msg.PreservedContentBlocks.Count, Is.EqualTo(1));
        }
    }
}
