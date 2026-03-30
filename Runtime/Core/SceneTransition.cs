#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace QAReporter.Core
{
    /// <summary>
    /// Records a scene transition that occurred during a bug report recording session.
    /// </summary>
    public class SceneTransition
    {
        /// <summary>
        /// The scene that was active before the transition.
        /// </summary>
        public string FromScene { get; set; }

        /// <summary>
        /// The scene that became active after the transition.
        /// </summary>
        public string ToScene { get; set; }

        /// <summary>
        /// When the transition occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}
#endif
