#if UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_QA_REPORTER
using Newtonsoft.Json;

namespace QAReporter.Jira
{
    /// <summary>
    /// Response from Jira REST API after creating an issue.
    /// </summary>
    public class JiraCreateResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("self")]
        public string Self { get; set; }

        /// <summary>
        /// The browse URL for the created ticket.
        /// </summary>
        public string GetBrowseUrl(string baseUrl)
        {
            return $"{baseUrl}/browse/{Key}";
        }
    }
}
#endif
