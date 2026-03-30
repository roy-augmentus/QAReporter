#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using QAReporter.Core;
using QAReporter.Jira;
using QAReporter.Screenshot;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QAReporter
{
    /// <summary>
    /// Orchestrates the bug report recording workflow.
    /// Singleton MonoBehaviour that persists across all scenes via DontDestroyOnLoad.
    /// </summary>
    public class BugReporterManager : MonoBehaviour
    {
        private static BugReporterManager _instance;

        /// <summary>
        /// Global singleton instance.
        /// </summary>
        public static BugReporterManager Instance => _instance;

        private LogRecorder _logRecorder;
        private BugReporterScreenshotCapturer _screenshotCapturer;
        private BugReportData _currentReport;
        private CancellationTokenSource _sendCts;
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

        private readonly ReactiveProperty<BugReporterState> _state =
            new ReactiveProperty<BugReporterState>(BugReporterState.Idle);

        /// <summary>
        /// Observable current state for UI binding.
        /// </summary>
        public IReadOnlyReactiveProperty<BugReporterState> State => _state;

        /// <summary>
        /// The current in-progress or completed bug report data.
        /// </summary>
        public BugReportData CurrentReport => _currentReport;

        /// <summary>
        /// The UIDocument used by the bug reporter UI. Set by BugReporterUIController.
        /// Used by the screenshot capturer to hide UI during capture.
        /// </summary>
        public UnityEngine.UIElements.UIDocument UIDocument { get; set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _logRecorder = new LogRecorder();
            _screenshotCapturer = new BugReporterScreenshotCapturer();
        }

        private void OnDestroy()
        {
            _logRecorder?.Dispose();
            _sendCts?.Cancel();
            _sendCts?.Dispose();

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (_state.Value != BugReporterState.Recording || _currentReport == null)
            {
                return;
            }

            // Track UI clicks during recording via EventSystem raycast.
            if (Input.GetMouseButtonDown(0) && EventSystem.current != null)
            {
                var eventData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };

                _raycastResults.Clear();
                EventSystem.current.RaycastAll(eventData, _raycastResults);

                if (_raycastResults.Count > 0)
                {
                    var hit = _raycastResults[0].gameObject;

                    // Skip bug reporter's own UI.
                    if (hit.transform.IsChildOf(transform))
                    {
                        return;
                    }

                    var interaction = new UIInteraction
                    {
                        Timestamp = DateTime.Now,
                        Description = DescribeUIElement(hit),
                        HierarchyPath = GetHierarchyPath(hit)
                    };

                    _currentReport.UIInteractions.Add(interaction);
                }
            }
        }

        private static string DescribeUIElement(GameObject go)
        {
            // Try to identify the element type and its label.
            var button = go.GetComponentInParent<Button>();
            if (button != null)
            {
                var label = GetUILabel(button.gameObject);
                return !string.IsNullOrEmpty(label)
                    ? $"Clicked Button '{label}'"
                    : $"Clicked Button '{button.gameObject.name}'";
            }

            var toggle = go.GetComponentInParent<Toggle>();
            if (toggle != null)
            {
                var label = GetUILabel(toggle.gameObject);
                return !string.IsNullOrEmpty(label)
                    ? $"Toggled '{label}' ({(toggle.isOn ? "On" : "Off")})"
                    : $"Toggled '{toggle.gameObject.name}' ({(toggle.isOn ? "On" : "Off")})";
            }

            var dropdown = go.GetComponentInParent<Dropdown>();
            if (dropdown != null)
            {
                return $"Opened Dropdown '{dropdown.gameObject.name}'";
            }

            var inputField = go.GetComponentInParent<InputField>();
            if (inputField != null)
            {
                return $"Focused InputField '{inputField.gameObject.name}'";
            }

            var slider = go.GetComponentInParent<Slider>();
            if (slider != null)
            {
                return $"Adjusted Slider '{slider.gameObject.name}' ({slider.value:F2})";
            }

            var scrollRect = go.GetComponentInParent<ScrollRect>();
            if (scrollRect != null)
            {
                return $"Scrolled '{scrollRect.gameObject.name}'";
            }

            return $"Clicked '{go.name}'";
        }

        private static string GetUILabel(GameObject go)
        {
            // Try TMP_Text first, then legacy Text.
            var tmp = go.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
            {
                return tmp.text.Length > 50 ? tmp.text.Substring(0, 50) + "..." : tmp.text;
            }

            var text = go.GetComponentInChildren<Text>();
            if (text != null && !string.IsNullOrWhiteSpace(text.text))
            {
                return text.text.Length > 50 ? text.text.Substring(0, 50) + "..." : text.text;
            }

            return null;
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            int depth = 0;

            while (parent != null && depth < 4)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
                depth++;
            }

            return path;
        }

        /// <summary>
        /// Begins a new recording session.
        /// </summary>
        public void StartRecording()
        {
            if (_state.Value != BugReporterState.Idle)
            {
                Debug.LogWarning("[BugReporter] Cannot start recording — not in Idle state.");
                return;
            }

            _currentReport = new BugReportData();
            _logRecorder.StartRecording();

            _currentReport.StartSceneName = _logRecorder.StartSceneName;
            _currentReport.StartTime = _logRecorder.StartTime;

            _state.Value = BugReporterState.Recording;
            Debug.Log("[BugReporter] Recording started.");
        }

        /// <summary>
        /// Captures a screenshot during recording.
        /// </summary>
        public async UniTask CaptureScreenshotAsync()
        {
            if (_state.Value != BugReporterState.Recording)
            {
                Debug.LogWarning("[BugReporter] Cannot capture screenshot — not recording.");
                return;
            }

            var screenshot = await _screenshotCapturer.CaptureAsync(UIDocument);
            _currentReport.Screenshots.Add(screenshot);
            Debug.Log($"[BugReporter] Screenshot captured ({_currentReport.Screenshots.Count} total).");
        }

        /// <summary>
        /// Stops recording and transitions to the Review state.
        /// </summary>
        public void StopRecording()
        {
            if (_state.Value != BugReporterState.Recording)
            {
                Debug.LogWarning("[BugReporter] Cannot stop recording — not recording.");
                return;
            }

            _logRecorder.StopRecording(out var logs, out var sceneTransitions);

            _currentReport.EndTime = DateTime.Now;
            _currentReport.Logs = logs;
            _currentReport.SceneTransitions = sceneTransitions;

            _state.Value = BugReporterState.Review;
            Debug.Log($"[BugReporter] Recording stopped. Captured {logs.Count} logs, " +
                      $"{sceneTransitions.Count} scene transitions, " +
                      $"{_currentReport.UIInteractions.Count} UI interactions, " +
                      $"{_currentReport.Screenshots.Count} screenshots.");
        }

        /// <summary>
        /// Submits the bug report to Jira.
        /// </summary>
        /// <returns>Tuple of (success, ticketKey, ticketUrl, error).</returns>
        public async UniTask<(bool success, string ticketKey, string ticketUrl, string error)>
            SubmitReportAsync()
        {
            if (_state.Value != BugReporterState.Review)
            {
                return (false, null, null, "Not in Review state.");
            }

            var settings = JiraSettings.Load();
            if (!settings.IsConfigured)
            {
                return (false, null, null, "Jira credentials not configured. Open Settings first.");
            }

            _state.Value = BugReporterState.Sending;
            _sendCts?.Cancel();
            _sendCts = new CancellationTokenSource();
            var ct = _sendCts.Token;

            try
            {
                var client = new JiraApiClient(settings);

                // Create the issue.
                var (response, createError) = await client.CreateIssueAsync(_currentReport, ct);
                if (response == null)
                {
                    _state.Value = BugReporterState.Error;
                    return (false, null, null, $"Failed to create ticket: {createError}");
                }

                Debug.Log($"[BugReporter] Ticket created: {response.Key}");

                // Attach console log as text file.
                var consoleLogText = _currentReport.GenerateConsoleLogText();
                var consoleLogBytes = System.Text.Encoding.UTF8.GetBytes(consoleLogText);
                var logFileName = $"console_log_{_currentReport.StartTime:yyyy-MM-dd_HH-mm-ss}.txt";
                var logAttachError = await client.AttachFileAsync(
                    response.Key, consoleLogBytes, logFileName, "text/plain", ct);

                if (logAttachError != null)
                {
                    Debug.LogWarning($"[BugReporter] Failed to attach console log: {logAttachError}");
                }

                // Attach screenshots.
                for (int i = 0; i < _currentReport.Screenshots.Count; i++)
                {
                    var screenshot = _currentReport.Screenshots[i];
                    var attachError = await client.AttachScreenshotAsync(
                        response.Key, screenshot, ct);

                    if (attachError != null)
                    {
                        Debug.LogWarning(
                            $"[BugReporter] Failed to attach screenshot {i + 1}: {attachError}");
                    }
                }

                var ticketUrl = response.GetBrowseUrl(settings.BaseUrl);
                _state.Value = BugReporterState.Complete;
                Debug.Log($"[BugReporter] Bug report submitted: {ticketUrl}");
                return (true, response.Key, ticketUrl, null);
            }
            catch (OperationCanceledException)
            {
                _state.Value = BugReporterState.Review;
                return (false, null, null, "Submission cancelled.");
            }
            catch (Exception e)
            {
                _state.Value = BugReporterState.Error;
                Debug.LogError($"[BugReporter] Submission error: {e}");
                return (false, null, null, e.Message);
            }
        }

        /// <summary>
        /// Submits the bug report as a comment on an existing Jira ticket.
        /// </summary>
        /// <param name="issueKey">The existing Jira issue key (e.g. "PROJ-123").</param>
        /// <returns>Tuple of (success, ticketUrl, error).</returns>
        public async UniTask<(bool success, string ticketUrl, string error)>
            SubmitAsCommentAsync(string issueKey)
        {
            if (_state.Value != BugReporterState.Review)
            {
                return (false, null, "Not in Review state.");
            }

            if (string.IsNullOrWhiteSpace(issueKey))
            {
                return (false, null, "Issue key is required.");
            }

            var settings = JiraSettings.Load();
            if (!settings.IsConfigured)
            {
                return (false, null, "Jira credentials not configured. Open Settings first.");
            }

            _state.Value = BugReporterState.Sending;
            _sendCts?.Cancel();
            _sendCts = new CancellationTokenSource();
            var ct = _sendCts.Token;

            try
            {
                var client = new JiraApiClient(settings);

                // Add the report as a comment.
                var commentText = _currentReport.GenerateMarkdownDescription();
                var commentError = await client.AddCommentAsync(issueKey, commentText, ct);
                if (commentError != null)
                {
                    _state.Value = BugReporterState.Error;
                    return (false, null, $"Failed to add comment: {commentError}");
                }

                Debug.Log($"[BugReporter] Comment added to {issueKey}");

                // Attach console log as text file.
                var consoleLogText = _currentReport.GenerateConsoleLogText();
                var consoleLogBytes = System.Text.Encoding.UTF8.GetBytes(consoleLogText);
                var logFileName = $"console_log_{_currentReport.StartTime:yyyy-MM-dd_HH-mm-ss}.txt";
                var logAttachError = await client.AttachFileAsync(
                    issueKey, consoleLogBytes, logFileName, "text/plain", ct);

                if (logAttachError != null)
                {
                    Debug.LogWarning($"[BugReporter] Failed to attach console log: {logAttachError}");
                }

                // Attach screenshots.
                for (int i = 0; i < _currentReport.Screenshots.Count; i++)
                {
                    var screenshot = _currentReport.Screenshots[i];
                    var attachError = await client.AttachScreenshotAsync(
                        issueKey, screenshot, ct);

                    if (attachError != null)
                    {
                        Debug.LogWarning(
                            $"[BugReporter] Failed to attach screenshot {i + 1}: {attachError}");
                    }
                }

                var ticketUrl = $"{settings.BaseUrl}/browse/{issueKey}";
                _state.Value = BugReporterState.Complete;
                Debug.Log($"[BugReporter] Comment submitted to: {ticketUrl}");
                return (true, ticketUrl, null);
            }
            catch (OperationCanceledException)
            {
                _state.Value = BugReporterState.Review;
                return (false, null, "Submission cancelled.");
            }
            catch (Exception e)
            {
                _state.Value = BugReporterState.Error;
                Debug.LogError($"[BugReporter] Comment submission error: {e}");
                return (false, null, e.Message);
            }
        }

        /// <summary>
        /// Cancels the current session and returns to Idle.
        /// </summary>
        public void Cancel()
        {
            _sendCts?.Cancel();

            if (_state.Value == BugReporterState.Recording)
            {
                _logRecorder.StopRecording(out _, out _);
            }

            _currentReport = null;
            _state.Value = BugReporterState.Idle;
        }

        /// <summary>
        /// Resets from Complete/Error state back to Idle for a new report.
        /// </summary>
        public void Reset()
        {
            _currentReport = null;
            _state.Value = BugReporterState.Idle;
        }
    }
}
#endif
