#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QAReporter.Core
{
    /// <summary>
    /// Captures console logs and scene transitions during a bug report recording session.
    /// Thread-safe: logs are captured from any thread via Application.logMessageReceivedThreaded.
    /// </summary>
    public class LogRecorder : IDisposable
    {
        private readonly ConcurrentQueue<LogEntry> _logQueue = new ConcurrentQueue<LogEntry>();
        private readonly List<SceneTransition> _sceneTransitions = new List<SceneTransition>();

        private bool _isRecording;
        private string _startSceneName;
        private DateTime _startTime;
        private bool _disposed;
        private int _insideCallback;

        /// <summary>
        /// Whether the recorder is currently capturing logs.
        /// </summary>
        public bool IsRecording => _isRecording;

        /// <summary>
        /// The scene that was active when recording started.
        /// </summary>
        public string StartSceneName => _startSceneName;

        /// <summary>
        /// When recording started.
        /// </summary>
        public DateTime StartTime => _startTime;

        public LogRecorder()
        {
            Application.logMessageReceivedThreaded += OnLogReceived;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        /// <summary>
        /// Begins capturing logs and scene transitions.
        /// </summary>
        public void StartRecording()
        {
            // Drain any stale entries from a previous session.
            while (_logQueue.TryDequeue(out _)) { }
            _sceneTransitions.Clear();

            _startSceneName = SceneManager.GetActiveScene().name;
            _startTime = DateTime.Now;
            _isRecording = true;
        }

        /// <summary>
        /// Stops capturing and returns all collected data.
        /// </summary>
        /// <param name="logs">All log entries captured during the recording window.</param>
        /// <param name="sceneTransitions">All scene transitions captured during the recording window.</param>
        public void StopRecording(out List<LogEntry> logs, out List<SceneTransition> sceneTransitions)
        {
            _isRecording = false;

            logs = new List<LogEntry>();
            while (_logQueue.TryDequeue(out var entry))
            {
                logs.Add(entry);
            }

            sceneTransitions = new List<SceneTransition>(_sceneTransitions);
            _sceneTransitions.Clear();
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            if (!_isRecording)
            {
                return;
            }

            // Guard against re-entrancy: if an exception occurs inside this callback,
            // Unity logs it which triggers OnLogReceived again recursively.
            if (System.Threading.Interlocked.CompareExchange(ref _insideCallback, 1, 0) != 0)
            {
                return;
            }

            try
            {
                var entry = new LogEntry
                {
                    Message = condition,
                    StackTrace = stackTrace,
                    Type = type,
                    Timestamp = DateTime.Now
                };

                // For error-level logs, capture an enhanced stack trace with file paths
                // and line numbers via System.Diagnostics. This gives Claude exact code
                // locations that Unity's default runtime stack trace omits.
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    try
                    {
                        var diagnosticTrace = new System.Diagnostics.StackTrace(true);
                        entry.EnhancedStackTrace = diagnosticTrace.ToString();
                    }
                    catch (Exception)
                    {
                        // System.Diagnostics.StackTrace may fail in some environments
                        // (e.g., IL2CPP with aggressive stripping). The standard
                        // Unity stack trace is still available as a fallback.
                    }
                }

                _logQueue.Enqueue(entry);
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _insideCallback, 0);
            }
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            if (!_isRecording)
            {
                return;
            }

            _sceneTransitions.Add(new SceneTransition
            {
                FromScene = previousScene.name,
                ToScene = newScene.name,
                Timestamp = DateTime.Now
            });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isRecording = false;
            Application.logMessageReceivedThreaded -= OnLogReceived;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }
    }
}
#endif
