#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace QAReporter.Core
{
    /// <summary>
    /// Records a user UI interaction captured during a bug report recording session.
    /// </summary>
    public class UIInteraction
    {
        /// <summary>
        /// When the interaction occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Human-readable description of what was clicked.
        /// e.g. "Button 'Start Scan'" or "Toggle 'Enable Preview'"
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The GameObject hierarchy path (e.g. "Canvas/Panel/StartButton").
        /// </summary>
        public string HierarchyPath { get; set; }
    }
}
#endif
