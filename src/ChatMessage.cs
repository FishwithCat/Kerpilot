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

        public ChatMessage(MessageSender sender, string text)
        {
            Sender = sender;
            Role = sender == MessageSender.User ? MessageRole.User : MessageRole.Assistant;
            Text = text;
            Timestamp = DateTime.Now;
        }

        private ChatMessage(MessageRole role, string text, List<ToolCall> toolCalls, string toolCallId)
        {
            Sender = role == MessageRole.User ? MessageSender.User : MessageSender.AI;
            Role = role;
            Text = text;
            ToolCalls = toolCalls;
            ToolCallId = toolCallId;
            Timestamp = DateTime.Now;
        }

        public static ChatMessage CreateAssistantToolCall(List<ToolCall> toolCalls, string content = null)
        {
            return new ChatMessage(MessageRole.Assistant, content, toolCalls, null);
        }

        public static ChatMessage CreateToolResult(string toolCallId, string content)
        {
            return new ChatMessage(MessageRole.Tool, content, null, toolCallId);
        }
    }
}
