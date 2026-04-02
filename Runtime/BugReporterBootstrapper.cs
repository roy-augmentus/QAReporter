#if UNITY_EDITOR || DEVELOPMENT_BUILD
using QAReporter.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace QAReporter
{
    /// <summary>
    /// Auto-initializes the BugReporterManager and UI at application startup.
    /// Creates a DontDestroyOnLoad GameObject with UIDocument — no prefab required.
    /// Requires PanelSettings asset at Resources/QAReporter/QAReporterPanelSettings.
    /// </summary>
    public static class BugReporterBootstrapper
    {
        private const string PanelSettingsPath = "QAReporter/QAReporterPanelSettings";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (BugReporterManager.Instance != null)
            {
                return;
            }

            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                Debug.LogWarning(
                    "[BugReporter] PanelSettings not found at Resources/" + PanelSettingsPath +
                    " — creating default PanelSettings at runtime.");
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.sortingOrder = 99999;
            }

            var go = new GameObject("[BugReporter]");
            go.AddComponent<BugReporterManager>();

            var uiDocument = go.AddComponent<UIDocument>();
            uiDocument.panelSettings = panelSettings;

            go.AddComponent<BugReporterUIController>();

            Debug.Log("[BugReporter] Initialized.");
        }
    }
}
#endif
