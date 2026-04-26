using System.Collections.Generic;
using NUnit.Framework;

namespace Kerpilot.Tests
{
    [TestFixture]
    public class GeminiProviderTests
    {
        // ── Provider detection ──

        [Test]
        public void Detect_GoogleNativeUrl_ReturnsGemini()
        {
            Assert.That(
                ChatProviderDetector.Detect("https://generativelanguage.googleapis.com/v1beta"),
                Is.EqualTo(ChatProvider.Gemini));
        }

        [Test]
        public void Detect_GoogleOpenAICompatUrl_ReturnsOpenAI()
        {
            // Google's OpenAI-compatible endpoint must NOT be classified as native Gemini
            Assert.That(
                ChatProviderDetector.Detect("https://generativelanguage.googleapis.com/v1beta/openai"),
                Is.EqualTo(ChatProvider.OpenAICompatible));
        }

        [Test]
        public void Detect_GeminiProxySuffix_ReturnsGemini()
        {
            Assert.That(
                ChatProviderDetector.Detect("https://proxy.example.com/gemini"),
                Is.EqualTo(ChatProvider.Gemini));
        }

        [Test]
        public void Detect_AnthropicUnchanged()
        {
            Assert.That(
                ChatProviderDetector.Detect("https://api.anthropic.com"),
                Is.EqualTo(ChatProvider.Anthropic));
        }

        // ── URL building ──

        [Test]
        public void BuildGeminiUrl_BareHost_AppendsV1beta()
        {
            string url = LlmClient.BuildGeminiUrl(
                "https://generativelanguage.googleapis.com", "gemini-2.0-flash");
            Assert.That(url, Is.EqualTo(
                "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:streamGenerateContent?alt=sse"));
        }

        [Test]
        public void BuildGeminiUrl_WithV1betaSuffix_PreservesIt()
        {
            string url = LlmClient.BuildGeminiUrl(
                "https://generativelanguage.googleapis.com/v1beta", "gemini-2.5-pro");
            Assert.That(url, Is.EqualTo(
                "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-pro:streamGenerateContent?alt=sse"));
        }

        // ── Tools array shape ──

        [Test]
        public void GetToolsJsonArrayGemini_WrapsInFunctionDeclarations()
        {
            string json = ToolDefinitions.GetToolsJsonArrayGemini();
            Assert.That(json, Does.StartWith("[{\"functionDeclarations\":["));
            Assert.That(json, Does.Contain("get_vessel_parts"));
            Assert.That(json, Does.Contain("\"parameters\":"));
            Assert.That(json, Does.Not.Contain("input_schema"));
            Assert.That(json, Does.Not.Contain("\"type\":\"function\""));
        }

        // ── Request body ──

        [Test]
        public void BuildGeminiRequestBody_HoistsSystemPrompt()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "hi")
            };
            string body = JsonHelper.BuildGeminiRequestBody(
                history, "gemini-2.0-flash", "You are helpful.", null);
            Assert.That(body, Does.Contain("\"systemInstruction\":{\"parts\":[{\"text\":\"You are helpful.\"}]}"));
            Assert.That(body, Does.Not.Contain("\"role\":\"system\""));
        }

        [Test]
        public void BuildGeminiRequestBody_AssistantRoleBecomesModel()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "hi"),
                new ChatMessage(MessageSender.AI, "hello")
            };
            string body = JsonHelper.BuildGeminiRequestBody(history, "gemini-2.0-flash", "sys", null);
            Assert.That(body, Does.Contain("\"role\":\"user\""));
            Assert.That(body, Does.Contain("\"role\":\"model\""));
            Assert.That(body, Does.Not.Contain("\"role\":\"assistant\""));
        }

        [Test]
        public void BuildGeminiRequestBody_ToolCallEmitsFunctionCallPart()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall("gemini_call_0_get_vessel_parts", "get_vessel_parts", "{}")
            };
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "Show parts"),
                ChatMessage.CreateAssistantToolCall(toolCalls),
                ChatMessage.CreateToolResult(
                    "gemini_call_0_get_vessel_parts",
                    "{\"vessel_name\":\"Test\"}",
                    "get_vessel_parts")
            };
            string body = JsonHelper.BuildGeminiRequestBody(
                history, "gemini-2.0-flash", "sys",
                ToolDefinitions.GetToolsJsonArrayGemini());

            Assert.That(body, Does.Contain("\"functionCall\":{\"name\":\"get_vessel_parts\""));
            Assert.That(body, Does.Contain("\"functionResponse\":{\"name\":\"get_vessel_parts\""));
            Assert.That(body, Does.Contain("\"response\":{\"vessel_name\":\"Test\"}"));
            Assert.That(body, Does.Contain("\"tools\":[{\"functionDeclarations\":["));
        }

        [Test]
        public void BuildGeminiRequestBody_CoalescesConsecutiveToolResults()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall("a", "fn_a", "{}"),
                new ToolCall("b", "fn_b", "{}")
            };
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "go"),
                ChatMessage.CreateAssistantToolCall(toolCalls),
                ChatMessage.CreateToolResult("a", "{\"r\":1}", "fn_a"),
                ChatMessage.CreateToolResult("b", "{\"r\":2}", "fn_b")
            };
            string body = JsonHelper.BuildGeminiRequestBody(history, "gemini-2.0-flash", "sys", null);

            // Two consecutive tool results must collapse into a single user turn
            // with two functionResponse parts (Gemini requires strict user/model
            // alternation).
            int firstUser = body.IndexOf("\"role\":\"user\"");
            int firstModel = body.IndexOf("\"role\":\"model\"");
            int secondUser = body.IndexOf("\"role\":\"user\"", firstUser + 1);
            int thirdUser = secondUser >= 0
                ? body.IndexOf("\"role\":\"user\"", secondUser + 1)
                : -1;

            Assert.That(firstUser, Is.GreaterThanOrEqualTo(0));
            Assert.That(firstModel, Is.GreaterThan(firstUser));
            Assert.That(secondUser, Is.GreaterThan(firstModel));
            Assert.That(thirdUser, Is.EqualTo(-1), "second tool result should not start a new user turn");
            Assert.That(body, Does.Contain("\"name\":\"fn_a\""));
            Assert.That(body, Does.Contain("\"name\":\"fn_b\""));
        }

        [Test]
        public void BuildGeminiRequestBody_NonObjectToolResult_WrappedAsContent()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall("c1", "fn", "{}")
            };
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "go"),
                ChatMessage.CreateAssistantToolCall(toolCalls),
                ChatMessage.CreateToolResult("c1", "plain text not json", "fn")
            };
            string body = JsonHelper.BuildGeminiRequestBody(history, "gemini-2.0-flash", "sys", null);
            Assert.That(body, Does.Contain("\"response\":{\"content\":\"plain text not json\"}"));
        }

        // ── Stream parser ──

        private static StreamDelta FirstContent(List<StreamDelta> deltas) =>
            deltas.Find(d => d.Content != null);

        private static StreamDelta FirstToolCall(List<StreamDelta> deltas) =>
            deltas.Find(d => d.HasToolCalls);

        private static List<StreamDelta> ToolCalls(List<StreamDelta> deltas) =>
            deltas.FindAll(d => d.HasToolCalls);

        [Test]
        public void ParseGeminiStreamEvents_TextPart_ExtractsContent()
        {
            string json = "{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"Hello\"}]},\"finishReason\":null,\"index\":0}]}";
            var deltas = JsonHelper.ParseGeminiStreamEvents(json);
            var content = FirstContent(deltas);
            Assert.That(content, Is.Not.Null);
            Assert.That(content.Content, Is.EqualTo("Hello"));
            Assert.That(FirstToolCall(deltas), Is.Null);
        }

        [Test]
        public void ParseGeminiStreamEvents_FunctionCall_ExtractsNameAndArgs()
        {
            string json = "{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"functionCall\":{\"name\":\"get_vessel_parts\",\"args\":{}}}]},\"index\":0}]}";
            var deltas = JsonHelper.ParseGeminiStreamEvents(json);
            var tc = FirstToolCall(deltas);
            Assert.That(tc, Is.Not.Null);
            Assert.That(tc.ToolCallFunctionName, Is.EqualTo("get_vessel_parts"));
            Assert.That(tc.ToolCallArguments, Is.EqualTo("{}"));
            Assert.That(tc.ToolCallId, Does.Contain("get_vessel_parts"));
        }

        [Test]
        public void ParseGeminiStreamEvents_FunctionCallWithArgs_PreservesArgsObject()
        {
            string json = "{\"candidates\":[{\"content\":{\"parts\":[{\"functionCall\":{\"name\":\"get_part_info\",\"args\":{\"part_name\":\"FL-T400\"}}}]}}]}";
            var deltas = JsonHelper.ParseGeminiStreamEvents(json);
            var tc = FirstToolCall(deltas);
            Assert.That(tc.ToolCallArguments, Is.EqualTo("{\"part_name\":\"FL-T400\"}"));
        }

        [Test]
        public void ParseGeminiStreamEvents_EveryPart_EmitsRawPreservation()
        {
            string json = "{\"candidates\":[{\"content\":{\"parts\":[" +
                "{\"text\":\"Hi\"}," +
                "{\"functionCall\":{\"name\":\"x\",\"args\":{}},\"thoughtSignature\":\"sig\"}" +
                "]}}]}";
            var deltas = JsonHelper.ParseGeminiStreamEvents(json);
            var preserved = deltas.FindAll(d => d.PreservedRawJson != null);
            Assert.That(preserved.Count, Is.EqualTo(2));
            Assert.That(preserved[0].PreservedRawJson, Is.EqualTo("{\"text\":\"Hi\"}"));
            Assert.That(preserved[1].PreservedRawJson, Does.Contain("thoughtSignature"));
            Assert.That(preserved[1].PreservedRawJson, Does.Contain("functionCall"));
        }

        [Test]
        public void ParseGeminiStreamEvents_ThoughtPart_SuppressesContentButPreservesRaw()
        {
            string json = "{\"candidates\":[{\"content\":{\"parts\":[" +
                "{\"thought\":true,\"text\":\"reasoning...\",\"thoughtSignature\":\"sig\"}" +
                "]}}]}";
            var deltas = JsonHelper.ParseGeminiStreamEvents(json);
            Assert.That(FirstContent(deltas), Is.Null,
                "thought-summary text must not surface in the visible stream");
            var preserved = deltas.Find(d => d.PreservedRawJson != null);
            Assert.That(preserved, Is.Not.Null);
            Assert.That(preserved.PreservedRawJson, Does.Contain("\"thought\":true"));
            Assert.That(preserved.PreservedRawJson, Does.Contain("\"thoughtSignature\":\"sig\""));
        }

        [Test]
        public void ParseGeminiStreamEvents_FunctionCallWithThoughtSignature_CapturesSignature()
        {
            string json = "{\"candidates\":[{\"content\":{\"parts\":[{" +
                "\"thoughtSignature\":\"sig-abc-123\"," +
                "\"functionCall\":{\"name\":\"get_contracts\",\"args\":{}}" +
                "}]}}]}";
            var deltas = JsonHelper.ParseGeminiStreamEvents(json);
            var tc = FirstToolCall(deltas);
            Assert.That(tc, Is.Not.Null);
            Assert.That(tc.ToolCallThoughtSignature, Is.EqualTo("sig-abc-123"));
        }

        [Test]
        public void BuildGeminiRequestBody_ToolCallWithSignature_EmitsThoughtSignature()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall("gemini_call_0_get_contracts", "get_contracts", "{}", "sig-abc-123")
            };
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "List contracts"),
                ChatMessage.CreateAssistantToolCall(toolCalls),
                ChatMessage.CreateToolResult(
                    "gemini_call_0_get_contracts", "{\"items\":[]}", "get_contracts")
            };
            string body = JsonHelper.BuildGeminiRequestBody(
                history, "gemini-2.5-pro", "sys", null);

            // Signature must appear as a sibling of functionCall on the same part.
            Assert.That(body, Does.Contain("\"thoughtSignature\":\"sig-abc-123\""));
            int sigIndex = body.IndexOf("\"thoughtSignature\":\"sig-abc-123\"");
            int fcIndex = body.IndexOf("\"functionCall\":{\"name\":\"get_contracts\"");
            Assert.That(sigIndex, Is.LessThan(fcIndex),
                "thoughtSignature must precede the functionCall block in the same part");
        }

        [Test]
        public void BuildGeminiRequestBody_PreservedParts_EchoedVerbatimAsPartsArray()
        {
            // Simulates a thinking-enabled turn: a thought-summary part with
            // its signature precedes the functionCall+signature part. Both must
            // round-trip unchanged or Gemini 400s the next request.
            var preserved = new List<string>
            {
                "{\"thought\":true,\"text\":\"reasoning summary\",\"thoughtSignature\":\"sig-thought\"}",
                "{\"functionCall\":{\"name\":\"get_contracts\",\"args\":{}},\"thoughtSignature\":\"sig-call\"}"
            };
            var toolCalls = new List<ToolCall>
            {
                new ToolCall("gemini_call_1_get_contracts", "get_contracts", "{}")
            };
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "List contracts"),
                ChatMessage.CreateAssistantToolCall(toolCalls, null, preserved),
                ChatMessage.CreateToolResult(
                    "gemini_call_1_get_contracts", "{\"items\":[]}", "get_contracts")
            };
            string body = JsonHelper.BuildGeminiRequestBody(
                history, "gemini-2.5-pro", "sys", null);

            Assert.That(body, Does.Contain("sig-thought"));
            Assert.That(body, Does.Contain("sig-call"));
            Assert.That(body, Does.Contain("\"thought\":true"));
            // Reconstruction path must not run (no extra functionCall block).
            int firstFc = body.IndexOf("\"functionCall\":{\"name\":\"get_contracts\"");
            int secondFc = firstFc >= 0
                ? body.IndexOf("\"functionCall\":{\"name\":\"get_contracts\"", firstFc + 1)
                : -1;
            Assert.That(secondFc, Is.EqualTo(-1),
                "preserved parts must replace reconstruction, not duplicate it");
        }

        [Test]
        public void BuildGeminiRequestBody_ToolCallWithoutSignature_OmitsField()
        {
            var toolCalls = new List<ToolCall>
            {
                new ToolCall("c1", "fn", "{}")
            };
            var history = new List<ChatMessage>
            {
                new ChatMessage(MessageSender.User, "go"),
                ChatMessage.CreateAssistantToolCall(toolCalls),
                ChatMessage.CreateToolResult("c1", "{}", "fn")
            };
            string body = JsonHelper.BuildGeminiRequestBody(history, "gemini-2.0-flash", "sys", null);
            Assert.That(body, Does.Not.Contain("thoughtSignature"));
        }

        [Test]
        public void ParseGeminiStreamEvents_ParallelFunctionCalls_EmitDistinctIndices()
        {
            string json = "{\"candidates\":[{\"content\":{\"parts\":[" +
                "{\"functionCall\":{\"name\":\"a\",\"args\":{}}}," +
                "{\"functionCall\":{\"name\":\"b\",\"args\":{}}}" +
                "]}}]}";
            var deltas = JsonHelper.ParseGeminiStreamEvents(json);
            var calls = ToolCalls(deltas);
            Assert.That(calls.Count, Is.EqualTo(2));
            Assert.That(calls[0].ToolCallFunctionName, Is.EqualTo("a"));
            Assert.That(calls[1].ToolCallFunctionName, Is.EqualTo("b"));
            Assert.That(calls[0].ToolCallIndex, Is.Not.EqualTo(calls[1].ToolCallIndex));
        }

        [Test]
        public void ParseGeminiStreamEvents_NoCandidates_ReturnsEmpty()
        {
            string json = "{\"usageMetadata\":{\"promptTokenCount\":10}}";
            var deltas = JsonHelper.ParseGeminiStreamEvents(json);
            Assert.That(deltas, Is.Empty);
        }

        [Test]
        public void ParseGeminiStreamEvents_NullOrEmpty_ReturnsEmpty()
        {
            Assert.That(JsonHelper.ParseGeminiStreamEvents(null), Is.Empty);
            Assert.That(JsonHelper.ParseGeminiStreamEvents(""), Is.Empty);
        }

        // ── ToolName preservation ──

        [Test]
        public void ChatMessage_CreateToolResult_StoresOptionalToolName()
        {
            var msg = ChatMessage.CreateToolResult("c1", "{}", "get_vessel_parts");
            Assert.That(msg.ToolName, Is.EqualTo("get_vessel_parts"));
        }

        [Test]
        public void ChatMessage_CreateToolResult_NameOptional_DefaultsNull()
        {
            var msg = ChatMessage.CreateToolResult("c1", "{}");
            Assert.That(msg.ToolName, Is.Null);
        }
    }
}
