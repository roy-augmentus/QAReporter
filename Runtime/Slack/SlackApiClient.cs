#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Text;
using System.Threading;
using QAReporter.Core;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace QAReporter.Slack
{
    /// <summary>
    /// Client for posting bug reports to Slack via the Web API.
    /// Uses Bot Token auth (xoxb-...).
    /// </summary>
    public class SlackApiClient
    {
        private const string BaseUrl = "https://slack.com/api";
        private readonly SlackSettings _settings;

        public SlackApiClient(SlackSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Tests the connection by calling auth.test.
        /// </summary>
        public async UniTask<(bool success, string error)> TestConnectionAsync(
            CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/auth.test";

            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Array.Empty<byte>()),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Authorization", $"Bearer {_settings.BotToken}");
            request.SetRequestHeader("Content-Type", "application/json");

            try
            {
                await request.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                return (false, $"Network error: {e.Message}");
            }

            var json = request.downloadHandler?.text;
            if (string.IsNullOrEmpty(json))
            {
                return (false, "Empty response");
            }

            var obj = JObject.Parse(json);
            if (obj["ok"]?.Value<bool>() == true)
            {
                return (true, null);
            }

            return (false, obj["error"]?.Value<string>() ?? "Unknown error");
        }

        /// <summary>
        /// Posts a bug report message to the configured Slack channel.
        /// Returns (success, messageTs, error). messageTs is used as the thread for file uploads.
        /// </summary>
        public async UniTask<(bool success, string messageTs, string error)> PostMessageAsync(
            string text, CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/chat.postMessage";

            var body = new JObject
            {
                ["channel"] = _settings.ChannelId,
                ["text"] = text,
                ["unfurl_links"] = false,
                ["unfurl_media"] = false
            };

            var jsonBytes = Encoding.UTF8.GetBytes(body.ToString(Formatting.None));

            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(jsonBytes) { contentType = "application/json; charset=utf-8" },
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Authorization", $"Bearer {_settings.BotToken}");

            try
            {
                await request.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                return (false, null, $"Network error: {e.Message}");
            }

            var json = request.downloadHandler?.text;
            if (string.IsNullOrEmpty(json))
            {
                return (false, null, "Empty response");
            }

            var obj = JObject.Parse(json);
            if (obj["ok"]?.Value<bool>() == true)
            {
                var ts = obj["ts"]?.Value<string>();
                return (true, ts, null);
            }

            return (false, null, obj["error"]?.Value<string>() ?? "Unknown error");
        }

        /// <summary>
        /// Uploads a file to Slack using files.upload (multipart form).
        /// Shares the file in the configured channel, threaded under the given messageTs.
        /// </summary>
        public async UniTask<string> UploadFileAsync(
            byte[] data, string fileName, string threadTs,
            CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/files.upload";

            var mimeType = fileName.EndsWith(".png") ? "image/png" : "text/plain";

            var form = new WWWForm();
            form.AddBinaryData("file", data, fileName, mimeType);
            form.AddField("channels", _settings.ChannelId);
            form.AddField("filename", fileName);
            form.AddField("title", fileName);

            if (!string.IsNullOrEmpty(threadTs))
            {
                form.AddField("thread_ts", threadTs);
            }

            using var request = UnityWebRequest.Post(url, form);
            request.SetRequestHeader("Authorization", $"Bearer {_settings.BotToken}");

            try
            {
                await request.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                return $"Network error: {e.Message}";
            }

            var json = request.downloadHandler?.text;
            if (string.IsNullOrEmpty(json))
            {
                return "Empty response";
            }

            var obj = JObject.Parse(json);
            if (obj["ok"]?.Value<bool>() == true)
            {
                Debug.Log($"[BugReporter] Uploaded {fileName} to Slack.");
                return null;
            }

            var error = obj["error"]?.Value<string>() ?? "Unknown error";
            Debug.LogError($"[BugReporter] Slack files.upload failed for {fileName}: {error}");
            return error;
        }
    }
}
#endif
