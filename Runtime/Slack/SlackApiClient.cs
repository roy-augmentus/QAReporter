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
        /// Uploads a file to Slack using the v2 upload API (getUploadURLExternal + completeUploadExternal).
        /// Shares the file in the configured channel, threaded under the given messageTs.
        /// </summary>
        public async UniTask<string> UploadFileAsync(
            byte[] data, string fileName, string threadTs,
            CancellationToken ct = default)
        {
            // Step 1: Get an upload URL.
            var getUrlResult = await GetUploadUrlAsync(fileName, data.Length, ct);
            if (getUrlResult.error != null)
            {
                return $"getUploadURLExternal: {getUrlResult.error}";
            }

            // Step 2: Upload file bytes to the URL.
            var uploadError = await UploadToUrlAsync(getUrlResult.uploadUrl, data, ct);
            if (uploadError != null)
            {
                return $"Upload: {uploadError}";
            }

            // Step 3: Complete the upload and share in channel.
            var completeError = await CompleteUploadAsync(
                getUrlResult.fileId, fileName, threadTs, ct);
            if (completeError != null)
            {
                return $"completeUploadExternal: {completeError}";
            }

            return null;
        }

        private async UniTask<(string uploadUrl, string fileId, string error)> GetUploadUrlAsync(
            string fileName, int length, CancellationToken ct)
        {
            var url = $"{BaseUrl}/files.getUploadURLExternal" +
                      $"?filename={UnityWebRequest.EscapeURL(fileName)}&length={length}";

            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {_settings.BotToken}");

            try
            {
                await request.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                return (null, null, $"Network error: {e.Message}");
            }

            var json = request.downloadHandler?.text;
            if (string.IsNullOrEmpty(json))
            {
                return (null, null, "Empty response");
            }

            var obj = JObject.Parse(json);
            if (obj["ok"]?.Value<bool>() == true)
            {
                var uploadUrl = obj["upload_url"]?.Value<string>();
                var fileId = obj["file_id"]?.Value<string>();
                return (uploadUrl, fileId, null);
            }

            return (null, null, obj["error"]?.Value<string>() ?? "Unknown error");
        }

        private static async UniTask<string> UploadToUrlAsync(
            string uploadUrl, byte[] data, CancellationToken ct)
        {
            using var request = new UnityWebRequest(uploadUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(data) { contentType = "application/octet-stream" },
                downloadHandler = new DownloadHandlerBuffer()
            };

            try
            {
                await request.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                return $"Network error: {e.Message}";
            }

            if (request.responseCode >= 200 && request.responseCode < 300)
            {
                return null;
            }

            return $"HTTP {request.responseCode}: {request.downloadHandler?.text}";
        }

        private async UniTask<string> CompleteUploadAsync(
            string fileId, string title, string threadTs, CancellationToken ct)
        {
            var url = $"{BaseUrl}/files.completeUploadExternal";

            var body = new JObject
            {
                ["files"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = fileId,
                        ["title"] = title
                    }
                },
                ["channel_id"] = _settings.ChannelId
            };

            if (!string.IsNullOrEmpty(threadTs))
            {
                body["thread_ts"] = threadTs;
            }

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
                return null;
            }

            return obj["error"]?.Value<string>() ?? "Unknown error";
        }
    }
}
#endif
