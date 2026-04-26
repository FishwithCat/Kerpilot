namespace Kerpilot
{
    public enum ChatProvider
    {
        OpenAICompatible,
        Anthropic,
        Gemini
    }

    public static class ChatProviderDetector
    {
        public static ChatProvider Detect(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl)) return ChatProvider.OpenAICompatible;

            string lower = baseUrl.ToLowerInvariant();
            string trimmed = lower.TrimEnd('/');

            // Google's OpenAI-compatible endpoint lives under
            // generativelanguage.googleapis.com/v1beta/openai/ — keep that on
            // the OpenAI path. Only the native streamGenerateContent route is Gemini.
            if (lower.Contains("generativelanguage.googleapis.com") && !lower.Contains("/openai"))
                return ChatProvider.Gemini;
            if (trimmed.EndsWith("/gemini")) return ChatProvider.Gemini;
            if (trimmed.EndsWith("/gemini/v1beta")) return ChatProvider.Gemini;

            if (lower.Contains("api.anthropic.com")) return ChatProvider.Anthropic;
            if (trimmed.EndsWith("/anthropic")) return ChatProvider.Anthropic;
            if (trimmed.EndsWith("/anthropic/v1")) return ChatProvider.Anthropic;

            return ChatProvider.OpenAICompatible;
        }
    }
}
