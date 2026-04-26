using System;
using System.Collections.Generic;

namespace Kerpilot
{
    public enum MessageSender
    {
        User,
        AI
    }

    public enum MessageRole
    {
        User,
        Assistant,
        System,
        Tool
    }

    public class ToolCall
    {
        public string Id { get; }
        public string FunctionName { get; }
        public string Arguments { get; }

        public ToolCall(string id, string functionName, string arguments)
        {
            Id = id;
            FunctionName = functionName;
            Arguments = arguments;
        }
    }

    public class ChatMessage
    {
        public MessageSender Sender { get; }
        public MessageRole Role { get; }
        public string Text { get; }
        public DateTime Timestamp { get; }
        public List<ToolCall> ToolCalls { get; }
        public string ToolCallId { get; }

        /// <summary>
        /// Function name a tool result corresponds to. OpenAI/Anthropic key
        /// tool results by id; Gemini's functionResponse blocks key by name,
        /// so we keep the original name on the result message and re-emit it
        /// when serializing for Gemini.
        /// </summary>
        public string ToolName { get; }

        /// <summary>
        /// Provider-opaque content-block JSON fragments that must be passed back
        /// unchanged in the next request. Used for Anthropic extended-thinking
        /// blocks (type "thinking" with signature, or "redacted_thinking") that
        /// accompany a tool_use turn — Anthropic 400s if these are stripped
        /// before the tool_result is sent.
        /// </summary>
        public List<string> PreservedContentBlocks { get; }

        public ChatMessage(MessageSender sender, string text)
        {
            Sender = sender;
            Role = sender == MessageSender.User ? MessageRole.User : MessageRole.Assistant;
            Text = text;
            Timestamp = DateTime.Now;
        }

        private ChatMessage(MessageRole role, string text, List<ToolCall> toolCalls, string toolCallId, string toolName, List<string> preservedBlocks)
        {
            Sender = role == MessageRole.User ? MessageSender.User : MessageSender.AI;
            Role = role;
            Text = text;
            ToolCalls = toolCalls;
            ToolCallId = toolCallId;
            ToolName = toolName;
            PreservedContentBlocks = preservedBlocks;
            Timestamp = DateTime.Now;
        }

        public static ChatMessage CreateAssistantToolCall(List<ToolCall> toolCalls, string content = null, List<string> preservedBlocks = null)
        {
            return new ChatMessage(MessageRole.Assistant, content, toolCalls, null, null, preservedBlocks);
        }

        public static ChatMessage CreateToolResult(string toolCallId, string content, string toolName = null)
        {
            return new ChatMessage(MessageRole.Tool, content, null, toolCallId, toolName, null);
        }
    }
}
