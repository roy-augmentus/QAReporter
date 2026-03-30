#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace QAReporter.Jira
{
    /// <summary>
    /// Stores Jira credentials and configuration. Persisted via PlayerPrefs.
    /// </summary>
    public class JiraSettings
    {
        private const string KeyEmail = "QAReporter_JiraEmail";
        private const string KeyApiToken = "QAReporter_JiraApiToken";
        private const string KeyCloudInstance = "QAReporter_JiraCloudInstance";
        private const string KeyProjectKey = "QAReporter_JiraProjectKey";

        private const string KeyIssueType = "QAReporter_JiraIssueType";

        private const string DefaultCloudInstance = "";
        private const string DefaultProjectKey = "";
        private const string DefaultIssueType = "Bug (Dev)";

        public string Email { get; set; } = "";
        public string ApiToken { get; set; } = "";
        public string CloudInstance { get; set; } = DefaultCloudInstance;
        public string ProjectKey { get; set; } = DefaultProjectKey;
        public string IssueType { get; set; } = DefaultIssueType;

        /// <summary>
        /// Whether the required credentials (email and API token) are configured.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(ApiToken);

        /// <summary>
        /// The base URL for Jira REST API calls.
        /// </summary>
        public string BaseUrl => $"https://{CloudInstance}";

        /// <summary>
        /// Loads settings from PlayerPrefs.
        /// </summary>
        public static JiraSettings Load()
        {
            return new JiraSettings
            {
                Email = PlayerPrefs.GetString(KeyEmail, ""),
                ApiToken = PlayerPrefs.GetString(KeyApiToken, ""),
                CloudInstance = PlayerPrefs.GetString(KeyCloudInstance, DefaultCloudInstance),
                ProjectKey = PlayerPrefs.GetString(KeyProjectKey, DefaultProjectKey),
                IssueType = PlayerPrefs.GetString(KeyIssueType, DefaultIssueType)
            };
        }

        /// <summary>
        /// Saves current settings to PlayerPrefs.
        /// </summary>
        public void Save()
        {
            PlayerPrefs.SetString(KeyEmail, Email);
            PlayerPrefs.SetString(KeyApiToken, ApiToken);
            PlayerPrefs.SetString(KeyCloudInstance, CloudInstance);
            PlayerPrefs.SetString(KeyProjectKey, ProjectKey);
            PlayerPrefs.SetString(KeyIssueType, IssueType);
            PlayerPrefs.Save();
        }
    }
}
#endif
