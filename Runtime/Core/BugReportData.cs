#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace QAReporter.Core
{
    /// <summary>
    /// Complete bug report data model. Contains both auto-captured and user-entered fields.
    /// Generates a Jira-ready markdown description optimized for Claude's /do-ticket command.
    /// </summary>
    public class BugReportData
    {
        // --- User-entered fields ---

        /// <summary>
        /// Bug ticket title (required).
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Free-text steps to reproduce the bug.
        /// </summary>
        public string StepsToReproduce { get; set; }

        /// <summary>
        /// What the user expected to happen.
        /// </summary>
        public string ExpectedBehavior { get; set; }

        /// <summary>
        /// What actually happened (important for visual bugs with no error log).
        /// </summary>
        public string ActualBehavior { get; set; }

        /// <summary>
        /// Optional test case identifier.
        /// </summary>
        public string TestCaseId { get; set; }

        // --- Auto-captured fields ---

        /// <summary>
        /// The scene that was active when recording started.
        /// </summary>
        public string StartSceneName { get; set; }

        /// <summary>
        /// When the recording started.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// When the recording ended.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// All console logs captured during the recording window.
        /// </summary>
        public List<LogEntry> Logs { get; set; } = new List<LogEntry>();

        /// <summary>
        /// Scene transitions that occurred during recording.
        /// </summary>
        public List<SceneTransition> SceneTransitions { get; set; } = new List<SceneTransition>();

        /// <summary>
        /// Screenshots captured by the user during recording.
        /// </summary>
        public List<ScreenshotData> Screenshots { get; set; } = new List<ScreenshotData>();

        /// <summary>
        /// UI interactions (clicks, toggles, etc.) captured during recording.
        /// </summary>
        public List<UIInteraction> UIInteractions { get; set; } = new List<UIInteraction>();

        /// <summary>
        /// Recording duration.
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// Generates a markdown description optimized for Jira and Claude's /do-ticket command.
        /// </summary>
        public string GenerateMarkdownDescription()
        {
            var sb = new StringBuilder();

            AppendStepsToReproduce(sb);
            AppendExpectedBehavior(sb);
            AppendActualBehavior(sb);
            AppendErrorLogs(sb);
            AppendTestCase(sb);
            AppendSystemInfo(sb);

            return sb.ToString();
        }

        private void AppendStepsToReproduce(StringBuilder sb)
        {
            sb.AppendLine("## Steps to Reproduce");
            sb.AppendLine($"**Scene:** {StartSceneName}");
            sb.AppendLine($"**Recording:** {StartTime:yyyy-MM-dd HH:mm:ss} → {EndTime:HH:mm:ss} ({Duration.TotalSeconds:F0}s)");
            sb.AppendLine();

            if (SceneTransitions.Count > 0)
            {
                sb.AppendLine("**Scene transitions during recording:**");
                foreach (var transition in SceneTransitions)
                {
                    sb.AppendLine($"- [{transition.Timestamp:HH:mm:ss}] {transition.FromScene} → {transition.ToScene}");
                }
                sb.AppendLine();
            }

            if (UIInteractions.Count > 0)
            {
                sb.AppendLine("**UI interactions during recording:**");
                foreach (var interaction in UIInteractions)
                {
                    sb.AppendLine($"- [{interaction.Timestamp:HH:mm:ss}] {interaction.Description} ({interaction.HierarchyPath})");
                }
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(StepsToReproduce))
            {
                sb.AppendLine(StepsToReproduce.Trim());
            }

            sb.AppendLine();
        }

        private void AppendExpectedBehavior(StringBuilder sb)
        {
            sb.AppendLine("## Expected Behavior");
            sb.AppendLine(!string.IsNullOrWhiteSpace(ExpectedBehavior)
                ? ExpectedBehavior.Trim()
                : "(not provided)");
            sb.AppendLine();
        }

        private void AppendActualBehavior(StringBuilder sb)
        {
            sb.AppendLine("## Actual Behavior");
            sb.AppendLine(!string.IsNullOrWhiteSpace(ActualBehavior)
                ? ActualBehavior.Trim()
                : "(not provided)");
            sb.AppendLine();
        }

        private void AppendErrorLogs(StringBuilder sb)
        {
            var errors = Logs.Where(l => l.IsError).ToList();
            sb.AppendLine("## Error Logs");

            if (errors.Count == 0)
            {
                sb.AppendLine("No errors captured during recording.");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("```");
            foreach (var error in errors)
            {
                sb.AppendLine($"[{error.Timestamp:HH:mm:ss}] {error.Type.ToString().ToUpper()} {error.Message}");

                var trace = error.BestStackTrace;
                if (!string.IsNullOrWhiteSpace(trace))
                {
                    // Indent stack trace lines for readability.
                    foreach (var line in trace.Split('\n'))
                    {
                        var trimmed = line.TrimEnd('\r');
                        if (!string.IsNullOrWhiteSpace(trimmed))
                        {
                            sb.AppendLine($"  {trimmed}");
                        }
                    }
                }

                sb.AppendLine();
            }
            sb.AppendLine("```");
            sb.AppendLine();
        }

        /// <summary>
        /// Generates the full console log as plain text for attachment.
        /// Includes all log entries with stack traces for errors.
        /// </summary>
        public string GenerateConsoleLogText()
        {
            if (Logs.Count == 0)
            {
                return "No console output captured during recording.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Console Log — {StartSceneName} — {StartTime:yyyy-MM-dd HH:mm:ss} to {EndTime:HH:mm:ss}");
            sb.AppendLine(new string('=', 80));
            sb.AppendLine();

            foreach (var log in Logs)
            {
                var typeLabel = log.Type switch
                {
                    LogType.Error => "ERROR",
                    LogType.Exception => "EXCEPTION",
                    LogType.Assert => "ASSERT",
                    LogType.Warning => "WARNING",
                    _ => "LOG"
                };

                sb.AppendLine($"[{log.Timestamp:HH:mm:ss}] {typeLabel} {log.Message}");

                if (log.IsError)
                {
                    var trace = log.BestStackTrace;
                    if (!string.IsNullOrWhiteSpace(trace))
                    {
                        foreach (var line in trace.Split('\n'))
                        {
                            var trimmed = line.TrimEnd('\r');
                            if (!string.IsNullOrWhiteSpace(trimmed))
                            {
                                sb.AppendLine($"  {trimmed}");
                            }
                        }
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private void AppendTestCase(StringBuilder sb)
        {
            if (!string.IsNullOrWhiteSpace(TestCaseId))
            {
                sb.AppendLine("## Test Case");
                sb.AppendLine(TestCaseId.Trim());
                sb.AppendLine();
            }
        }

        private void AppendSystemInfo(StringBuilder sb)
        {
            sb.AppendLine("## System");
            sb.AppendLine($"Unity {Application.unityVersion} | {SystemInfo.operatingSystem} | {SystemInfo.graphicsDeviceName}");
        }
    }
}
#endif
