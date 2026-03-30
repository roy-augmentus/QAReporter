#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using QAReporter.Core;
using Newtonsoft.Json.Linq;

namespace QAReporter.Jira
{
    /// <summary>
    /// Builds Jira REST API v3 Atlassian Document Format (ADF) JSON
    /// from a BugReportData instance.
    /// </summary>
    public static class AdfDocumentBuilder
    {
        /// <summary>
        /// Builds the complete ADF document for a bug report.
        /// </summary>
        public static JObject Build(BugReportData data)
        {
            var content = new JArray();

            AddStepsToReproduce(content, data);
            AddExpectedBehavior(content, data);
            AddActualBehavior(content, data);
            AddErrorLogs(content, data);
            AddConsoleOutput(content, data);
            AddTestCase(content, data);
            AddSystemInfo(content, data);

            return new JObject
            {
                ["type"] = "doc",
                ["version"] = 1,
                ["content"] = content
            };
        }

        private static void AddStepsToReproduce(JArray content, BugReportData data)
        {
            content.Add(Heading("Steps to Reproduce", 2));
            content.Add(Paragraph(
                Bold("Scene: "), Text(data.StartSceneName),
                HardBreak(),
                Bold("Recording: "),
                Text($"{data.StartTime:yyyy-MM-dd HH:mm:ss} → {data.EndTime:HH:mm:ss} ({data.Duration.TotalSeconds:F0}s)")
            ));

            if (data.SceneTransitions.Count > 0)
            {
                content.Add(Paragraph(Bold("Scene transitions during recording:")));
                var items = data.SceneTransitions.Select(t =>
                    BulletItem($"[{t.Timestamp:HH:mm:ss}] {t.FromScene} → {t.ToScene}"));
                content.Add(BulletList(items));
            }

            if (!string.IsNullOrWhiteSpace(data.StepsToReproduce))
            {
                content.Add(Paragraph(Text(data.StepsToReproduce.Trim())));
            }
        }

        private static void AddExpectedBehavior(JArray content, BugReportData data)
        {
            content.Add(Heading("Expected Behavior", 2));
            content.Add(Paragraph(Text(
                !string.IsNullOrWhiteSpace(data.ExpectedBehavior)
                    ? data.ExpectedBehavior.Trim()
                    : "(not provided)")));
        }

        private static void AddActualBehavior(JArray content, BugReportData data)
        {
            content.Add(Heading("Actual Behavior", 2));
            content.Add(Paragraph(Text(
                !string.IsNullOrWhiteSpace(data.ActualBehavior)
                    ? data.ActualBehavior.Trim()
                    : "(not provided)")));
        }

        private static void AddErrorLogs(JArray content, BugReportData data)
        {
            var errors = data.Logs.Where(l => l.IsError).ToList();
            content.Add(Heading("Error Logs", 2));

            if (errors.Count == 0)
            {
                content.Add(Paragraph(Text("No errors captured during recording.")));
                return;
            }

            var errorText = string.Join("\n\n", errors.Select(e =>
            {
                var entry = $"[{e.Timestamp:HH:mm:ss}] {e.Type.ToString().ToUpper()} {e.Message}";
                var trace = e.BestStackTrace;
                if (!string.IsNullOrWhiteSpace(trace))
                {
                    var indented = string.Join("\n",
                        trace.Split('\n')
                            .Select(l => l.TrimEnd('\r'))
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .Select(l => $"  {l}"));
                    entry += "\n" + indented;
                }
                return entry;
            }));

            content.Add(CodeBlock(errorText));
        }

        private static void AddConsoleOutput(JArray content, BugReportData data)
        {
            if (data.Logs.Count == 0)
            {
                return;
            }

            content.Add(Heading("Console Output", 2));

            const int maxChars = 24000;
            var lines = new List<string>();
            int totalChars = 0;

            foreach (var log in data.Logs)
            {
                var typeLabel = log.IsError ? log.Type.ToString().ToUpper()
                    : log.Type == UnityEngine.LogType.Warning ? "WARNING"
                    : "LOG";

                var line = $"[{log.Timestamp:HH:mm:ss}] {typeLabel} {log.Message}";

                if (totalChars + line.Length > maxChars)
                {
                    int remaining = data.Logs.Count - lines.Count;
                    lines.Add($"... ({remaining} more log entries truncated, Jira description limit)");
                    break;
                }

                lines.Add(line);
                totalChars += line.Length;
            }

            content.Add(CodeBlock(string.Join("\n", lines)));
        }

        private static void AddTestCase(JArray content, BugReportData data)
        {
            if (string.IsNullOrWhiteSpace(data.TestCaseId))
            {
                return;
            }

            content.Add(Heading("Test Case", 2));
            content.Add(Paragraph(Text(data.TestCaseId.Trim())));
        }

        private static void AddSystemInfo(JArray content, BugReportData data)
        {
            content.Add(Heading("System", 2));
            content.Add(Paragraph(Text(
                $"Unity {UnityEngine.Application.unityVersion} | " +
                $"{UnityEngine.SystemInfo.operatingSystem} | " +
                $"{UnityEngine.SystemInfo.graphicsDeviceName}")));
        }

        // --- ADF node helpers ---

        private static JObject Heading(string text, int level)
        {
            return new JObject
            {
                ["type"] = "heading",
                ["attrs"] = new JObject { ["level"] = level },
                ["content"] = new JArray { TextNode(text) }
            };
        }

        private static JObject Paragraph(params JObject[] inlineNodes)
        {
            return new JObject
            {
                ["type"] = "paragraph",
                ["content"] = new JArray(inlineNodes.Cast<object>().ToArray())
            };
        }

        private static JObject CodeBlock(string text)
        {
            return new JObject
            {
                ["type"] = "codeBlock",
                ["attrs"] = new JObject { ["language"] = "text" },
                ["content"] = new JArray { TextNode(text) }
            };
        }

        private static JObject BulletList(IEnumerable<JObject> items)
        {
            return new JObject
            {
                ["type"] = "bulletList",
                ["content"] = new JArray(items.Cast<object>().ToArray())
            };
        }

        private static JObject BulletItem(string text)
        {
            return new JObject
            {
                ["type"] = "listItem",
                ["content"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "paragraph",
                        ["content"] = new JArray { TextNode(text) }
                    }
                }
            };
        }

        private static JObject TextNode(string text)
        {
            return new JObject
            {
                ["type"] = "text",
                ["text"] = text
            };
        }

        private static JObject Text(string text) => TextNode(text);

        private static JObject Bold(string text)
        {
            return new JObject
            {
                ["type"] = "text",
                ["text"] = text,
                ["marks"] = new JArray
                {
                    new JObject { ["type"] = "strong" }
                }
            };
        }

        private static JObject HardBreak()
        {
            return new JObject { ["type"] = "hardBreak" };
        }
    }
}
#endif
