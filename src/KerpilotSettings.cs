using System.IO;

namespace Kerpilot
{
    public class KerpilotSettings
    {
        private const string DefaultBaseUrl = "https://api.openai.com/v1";
        private const string DefaultModel = "gpt-4";
        private const string KeyBaseUrl = "BaseUrl";
        private const string KeyApiKey = "ApiKey";
        private const string KeyModelName = "ModelName";
        private const string NodeName = "KerpilotSettings";

        public string BaseUrl { get; set; } = DefaultBaseUrl;
        public string ApiKey { get; set; } = "";
        public string ModelName { get; set; } = DefaultModel;

        public bool IsConfigured => !string.IsNullOrEmpty(ApiKey);

        private static string SettingsPath =>
            Path.Combine(KSPUtil.ApplicationRootPath, "GameData/Kerpilot/PluginData/settings.cfg");

        public void Save()
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var node = new ConfigNode(NodeName);
            node.AddValue(KeyBaseUrl, Encode(BaseUrl));
            node.AddValue(KeyApiKey, Encode(ApiKey));
            node.AddValue(KeyModelName, Encode(ModelName));

            var root = new ConfigNode();
            root.AddNode(node);
            root.Save(SettingsPath);
        }

        public static KerpilotSettings Load()
        {
            var settings = new KerpilotSettings();
            if (!File.Exists(SettingsPath))
                return settings;

            var root = ConfigNode.Load(SettingsPath);
            if (root == null) return settings;

            var node = root.GetNode(NodeName);
            if (node == null) return settings;

            if (node.HasValue(KeyBaseUrl))
                settings.BaseUrl = Decode(node.GetValue(KeyBaseUrl));
            if (node.HasValue(KeyApiKey))
                settings.ApiKey = Decode(node.GetValue(KeyApiKey));
            if (node.HasValue(KeyModelName))
                settings.ModelName = Decode(node.GetValue(KeyModelName));

            return settings;
        }

        // ConfigNode treats "//" as comment and strips it, so we must encode values
        private static string Encode(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Replace("\\", "\\\\").Replace("//", "\\/\\/");
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Replace("\\/\\/", "//").Replace("\\\\", "\\");
        }
    }
}
