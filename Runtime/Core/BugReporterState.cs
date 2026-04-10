#if UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_QA_REPORTER
namespace QAReporter.Core
{
    /// <summary>
    /// States for the bug reporter workflow state machine.
    /// </summary>
    public enum BugReporterState
    {
        Idle,
        Recording,
        Review,
        Sending,
        Complete,
        Error
    }
}
#endif
