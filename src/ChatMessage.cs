using System;

namespace Kerpilot
{
    public enum MessageSender
    {
        User,
        AI
    }

    public class ChatMessage
    {
        public MessageSender Sender { get; }
        public string Text { get; }
        public DateTime Timestamp { get; }

        public ChatMessage(MessageSender sender, string text)
        {
            Sender = sender;
            Text = text;
            Timestamp = DateTime.Now;
        }
    }
}
