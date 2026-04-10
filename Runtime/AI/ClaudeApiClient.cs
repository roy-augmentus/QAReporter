#if UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_QA_REPORTER
using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace QAReporter.AI
{
    public class ClaudeApiClient
    {
        private const string ApiUrl = "https://api.anthropic.com/v1/messages";
        private const string ApiVersion = "2023-06-01";
        private const string Model = "claude-haiku-4-5-20251001";

        private readonly string _apiKey;

        public ClaudeApiClient(string apiKey)
        {
            _apiKey = apiKey;
        }

        public async UniTask<(string title, string error)> SuggestTitleAsync(
            string expectedBehavior, string actualBehavior, CancellationToken ct = default)
        {
            var prompt = "You are a QA bug title generator. Based on the expected and actual behavior below, " +
                         "write a single concise bug title (under 80 characters). " +
                         "Reply with ONLY the title, nothing else.\n\n" +
                         $"Expected: {expectedBehavior}\n" +
                         $"Actual: {actualBehavior}";

            var body = new JObject
            {
                ["model"] = Model,
                ["max_tokens"] = 100,
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = prompt
                    }
                }
            };

            var jsonBytes = Encoding.UTF8.GetBytes(body.ToString(Formatting.None));

            using var request = new UnityWebRequest(ApiUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(jsonBytes) { contentType = "application/json" },
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("x-api-key", _apiKey);
            request.SetRequestHeader("anthropic-version", ApiVersion);

            try
            {
                await request.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                return (null, $"Network error: {e.Message}");
            }

            if (request.responseCode != 200)
            {
                var errorText = request.downloadHandler?.text;
                Debug.LogError($"[BugReporter] Claude API error: HTTP {request.responseCode} - {errorText}");
                return (null, $"HTTP {request.responseCode}");
            }

            try
            {
                var response = JObject.Parse(request.downloadHandler.text);
                var text = response["content"]?[0]?["text"]?.ToString()?.Trim();
                return (text, null);
            }
            catch (Exception e)
            {
                return (null, $"Failed to parse response: {e.Message}");
            }
        }
    }
}
#endif
