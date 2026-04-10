#if UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_QA_REPORTER
using System;

namespace QAReporter.Core
{
    /// <summary>
    /// Stores a screenshot captured during a bug report recording session.
    /// </summary>
    public class ScreenshotData
    {
        /// <summary>
        /// PNG-encoded screenshot bytes.
        /// </summary>
        public byte[] PngData { get; set; }

        /// <summary>
        /// When the screenshot was captured.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Auto-generated filename for Jira attachment.
        /// </summary>
        public string FileName => $"screenshot_{Timestamp:yyyy-MM-dd_HH-mm-ss}.png";
    }
}
#endif
