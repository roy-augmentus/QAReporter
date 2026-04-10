#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace QAReporter.Slack
{
    /// <summary>
    /// Stores Slack credentials and configuration. Persisted via PlayerPrefs.
    /// </summary>
    public class SlackSettings
    {
        private const string KeyBotToken = "QAReporter_SlackBotToken";
        private const string KeyChannelId = "QAReporter_SlackChannelId";

        public string BotToken { get; set; } = "";
        public string ChannelId { get; set; } = "";

        /// <summary>
        /// Whether the required Slack credentials are configured.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ChannelId);

        /// <summary>
        /// Loads settings from PlayerPrefs.
        /// </summary>
        public static SlackSettings Load()
        {
            return new SlackSettings
            {
                BotToken = PlayerPrefs.GetString(KeyBotToken, ""),
                ChannelId = PlayerPrefs.GetString(KeyChannelId, "")
            };
        }

        /// <summary>
        /// Saves current settings to PlayerPrefs.
        /// </summary>
        public void Save()
        {
            PlayerPrefs.SetString(KeyBotToken, BotToken);
            PlayerPrefs.SetString(KeyChannelId, ChannelId);
            PlayerPrefs.Save();
        }
    }
}
#endif
