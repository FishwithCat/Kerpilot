namespace Kerpilot
{
    public enum ChatProvider
    {
        OpenAICompatible,
        Anthropic
    }

    public static class ChatProviderDetector
    {
        public static ChatProvider Detect(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl)) return ChatProvider.OpenAICompatible;

            string lower = baseUrl.ToLowerInvariant();
            string trimmed = lower.TrimEnd('/');

            if (lower.Contains("api.anthropic.com")) return ChatProvider.Anthropic;
            if (trimmed.EndsWith("/anthropic")) return ChatProvider.Anthropic;
            if (trimmed.EndsWith("/anthropic/v1")) return ChatProvider.Anthropic;

            return ChatProvider.OpenAICompatible;
        }
    }
}
