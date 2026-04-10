#if UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_QA_REPORTER
using System;
using System.Text;
using System.Threading;
using QAReporter.Core;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
// AdfDocumentBuilder kept for potential future v3 API migration.
using UnityEngine;
using UnityEngine.Networking;

namespace QAReporter.Jira
{
    /// <summary>
    /// Client for creating bug tickets and attaching files via the Jira REST API v3.
    /// Uses HTTP Basic auth (email:apiToken).
    /// </summary>
    public class JiraApiClient
    {
        private readonly JiraSettings _settings;
        private readonly string _authHeader;

        public JiraApiClient(JiraSettings settings)
        {
            _settings = settings;
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{settings.Email}:{settings.ApiToken}"));
            _authHeader = $"Basic {credentials}";
        }

        /// <summary>
        /// Tests the connection by calling /rest/api/2/myself.
        /// Returns true if the credentials are valid.
        /// </summary>
        public async UniTask<(bool success, string error)> TestConnectionAsync(
            CancellationToken ct = default)
        {
            var url = $"{_settings.BaseUrl}/rest/api/2/myself";

            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", _authHeader);
            request.SetRequestHeader("Accept", "application/json");

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
                return (false, $"Network error: {e.Message}");
            }

            if (request.responseCode == 200)
            {
                return (true, null);
            }

            return (false, $"HTTP {request.responseCode}: {request.downloadHandler?.text}");
        }

        /// <summary>
        /// Creates a Bug issue in Jira and returns the response with issue key.
        /// Uses a two-step approach: create with summary only, then PUT description.
        /// This is more reliable than setting description on creation.
        /// </summary>
        public async UniTask<(JiraCreateResponse response, string error)> CreateIssueAsync(
            BugReportData data, CancellationToken ct = default)
        {
            // Step 1: Create the issue with summary only.
            var createUrl = $"{_settings.BaseUrl}/rest/api/2/issue";

            var createBody = new JObject
            {
                ["fields"] = new JObject
                {
                    ["project"] = new JObject { ["key"] = _settings.ProjectKey },
                    ["issuetype"] = new JObject { ["name"] = _settings.IssueType },
                    ["summary"] = data.Title
                }
            };

            var createJsonBytes = Encoding.UTF8.GetBytes(createBody.ToString(Formatting.None));

            JiraCreateResponse createResponse;
            using (var request = new UnityWebRequest(createUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(createJsonBytes) { contentType = "application/json" },
                downloadHandler = new DownloadHandlerBuffer()
            })
            {
                request.SetRequestHeader("Authorization", _authHeader);
                request.SetRequestHeader("Accept", "application/json");

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

                var responseText = request.downloadHandler?.text;

                if (request.responseCode != 201)
                {
                    return (null, $"HTTP {request.responseCode}: {ParseErrorMessage(responseText)}");
                }

                createResponse = JsonConvert.DeserializeObject<JiraCreateResponse>(responseText);
            }

            // Step 2: Update the description via PUT.
            // Delay to let Jira's post-creation workflow (default template) finish first,
            // otherwise our PUT gets overwritten by the Bug (Dev) template.
            var description = data.GenerateMarkdownDescription();
            Debug.Log($"[BugReporter] Step 2: Waiting for Jira template to settle before updating {createResponse.Key}...");
            await UniTask.Delay(3000, cancellationToken: ct);

            Debug.Log($"[BugReporter] Updating description on {createResponse.Key} ({description.Length} chars)");
            var descriptionError = await UpdateDescriptionAsync(
                createResponse.Key, description, ct);

            if (descriptionError != null)
            {
                Debug.LogError($"[BugReporter] Description update failed for {createResponse.Key}: {descriptionError}");
            }
            else
            {
                Debug.Log($"[BugReporter] Description updated successfully on {createResponse.Key}");
            }

            return (createResponse, null);
        }

        /// <summary>
        /// Updates the description of an existing Jira issue via PUT.
        /// </summary>
        private async UniTask<string> UpdateDescriptionAsync(
            string issueKey, string description, CancellationToken ct)
        {
            var url = $"{_settings.BaseUrl}/rest/api/2/issue/{issueKey}";

            var body = new JObject
            {
                ["fields"] = new JObject
                {
                    ["description"] = description
                }
            };

            var jsonBytes = Encoding.UTF8.GetBytes(body.ToString(Formatting.None));

            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT)
            {
                uploadHandler = new UploadHandlerRaw(jsonBytes) { contentType = "application/json" },
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Authorization", _authHeader);
            request.SetRequestHeader("Accept", "application/json");

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
                return $"Network error: {e.Message}";
            }

            Debug.Log($"[BugReporter] PUT response: {request.responseCode}");

            // PUT returns 204 No Content on success.
            if (request.responseCode == 204)
            {
                return null;
            }

            var errorBody = request.downloadHandler?.text;
            Debug.LogError($"[BugReporter] PUT failed: {request.responseCode} - {errorBody}");
            return $"HTTP {request.responseCode}: {ParseErrorMessage(errorBody)}";
        }

        /// <summary>
        /// Attaches a screenshot to an existing Jira issue.
        /// </summary>
        public async UniTask<string> AttachScreenshotAsync(
            string issueKey, ScreenshotData screenshot, CancellationToken ct = default)
        {
            var url = $"{_settings.BaseUrl}/rest/api/2/issue/{issueKey}/attachments";

            var formData = new WWWForm();
            formData.AddBinaryData("file", screenshot.PngData, screenshot.FileName, "image/png");

            Debug.Log($"[BugReporter] Attaching screenshot {screenshot.FileName} ({screenshot.PngData.Length} bytes) to {issueKey}");

            using var request = UnityWebRequest.Post(url, formData);
            request.SetRequestHeader("Authorization", _authHeader);
            request.SetRequestHeader("X-Atlassian-Token", "no-check");
            request.SetRequestHeader("Accept", "application/json");

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
                Debug.LogError($"[BugReporter] Attach screenshot {screenshot.FileName} network error: {e.Message}");
                return $"Network error: {e.Message}";
            }

            if (request.responseCode == 200 || request.responseCode == 201)
            {
                Debug.Log($"[BugReporter] Attached screenshot {screenshot.FileName} successfully");
                return null;
            }

            var errorText = request.downloadHandler?.text;
            Debug.LogError($"[BugReporter] Attach screenshot failed: HTTP {request.responseCode} - {errorText}");
            return $"HTTP {request.responseCode}: {ParseErrorMessage(errorText)}";
        }

        /// <summary>
        /// Attaches a file to an existing Jira issue.
        /// </summary>
        public async UniTask<string> AttachFileAsync(
            string issueKey, byte[] data, string fileName, string mimeType,
            CancellationToken ct = default)
        {
            var url = $"{_settings.BaseUrl}/rest/api/2/issue/{issueKey}/attachments";

            var formData = new WWWForm();
            formData.AddBinaryData("file", data, fileName, mimeType);

            Debug.Log($"[BugReporter] Attaching {fileName} ({data.Length} bytes) to {issueKey}");

            using var request = UnityWebRequest.Post(url, formData);
            request.SetRequestHeader("Authorization", _authHeader);
            request.SetRequestHeader("X-Atlassian-Token", "no-check");
            request.SetRequestHeader("Accept", "application/json");

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
                Debug.LogError($"[BugReporter] Attach {fileName} network error: {e.Message}");
                return $"Network error: {e.Message}";
            }

            if (request.responseCode == 200 || request.responseCode == 201)
            {
                Debug.Log($"[BugReporter] Attached {fileName} successfully");
                return null;
            }

            var errorText = request.downloadHandler?.text;
            Debug.LogError($"[BugReporter] Attach {fileName} failed: HTTP {request.responseCode} - {errorText}");
            return $"HTTP {request.responseCode}: {ParseErrorMessage(errorText)}";
        }

        /// <summary>
        /// Adds a plain-text comment to an existing Jira issue.
        /// </summary>
        public async UniTask<string> AddCommentAsync(
            string issueKey, string commentBody, CancellationToken ct = default)
        {
            var url = $"{_settings.BaseUrl}/rest/api/2/issue/{issueKey}/comment";

            var body = new JObject
            {
                ["body"] = commentBody
            };

            var jsonBytes = Encoding.UTF8.GetBytes(body.ToString(Formatting.None));

            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(jsonBytes) { contentType = "application/json" },
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Authorization", _authHeader);
            request.SetRequestHeader("Accept", "application/json");

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
                return $"Network error: {e.Message}";
            }

            if (request.responseCode == 200 || request.responseCode == 201)
            {
                return null;
            }

            var errorText = request.downloadHandler?.text;
            Debug.LogError($"[BugReporter] AddComment failed: HTTP {request.responseCode} - {errorText}");
            return $"HTTP {request.responseCode}: {ParseErrorMessage(errorText)}";
        }

        private static string ParseErrorMessage(string responseJson)
        {
            if (string.IsNullOrEmpty(responseJson))
            {
                return "Empty response";
            }

            try
            {
                var obj = JObject.Parse(responseJson);

                // Jira errors can be in "errorMessages" array or "errors" object.
                var messages = obj["errorMessages"]?.ToObject<string[]>();
                if (messages != null && messages.Length > 0)
                {
                    return string.Join("; ", messages);
                }

                var errors = obj["errors"];
                if (errors != null && errors.HasValues)
                {
                    return errors.ToString(Formatting.None);
                }
            }
            catch
            {
                // Not JSON, return raw.
            }

            return responseJson.Length > 200 ? responseJson.Substring(0, 200) + "..." : responseJson;
        }
    }
}
#endif
