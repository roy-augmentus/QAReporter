#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace QAReporter.Core
{
    /// <summary>
    /// A single console log entry captured during a bug report recording session.
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// The log message text.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Unity's default stack trace (may lack file paths in builds).
        /// </summary>
        public string StackTrace { get; set; }

        /// <summary>
        /// Enhanced stack trace captured via System.Diagnostics.StackTrace(true).
        /// Contains file paths and line numbers when available.
        /// Only populated for Error, Exception, and Assert log types.
        /// </summary>
        public string EnhancedStackTrace { get; set; }

        /// <summary>
        /// The Unity log type (Log, Warning, Error, Exception, Assert).
        /// </summary>
        public LogType Type { get; set; }

        /// <summary>
        /// When this log entry was captured.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Returns true if this is an error-level log entry.
        /// </summary>
        public bool IsError =>
            Type == LogType.Error || Type == LogType.Exception || Type == LogType.Assert;

        /// <summary>
        /// Returns the best available stack trace, preferring the enhanced version.
        /// </summary>
        public string BestStackTrace =>
            !string.IsNullOrEmpty(EnhancedStackTrace) ? EnhancedStackTrace : StackTrace;
    }
}
#endif
