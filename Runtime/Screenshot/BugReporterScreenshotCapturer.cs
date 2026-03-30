#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using QAReporter.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace QAReporter.Screenshot
{
    /// <summary>
    /// Captures screenshots during bug report recording.
    /// Hides the bug reporter UIDocument before capture and re-shows it after.
    /// </summary>
    public class BugReporterScreenshotCapturer
    {
        /// <summary>
        /// Captures a screenshot, temporarily hiding the provided UIDocument.
        /// </summary>
        /// <param name="uiDocument">The bug reporter UIDocument to hide during capture. May be null.</param>
        /// <returns>The captured screenshot data.</returns>
        public async UniTask<ScreenshotData> CaptureAsync(UIDocument uiDocument)
        {
            if (uiDocument != null)
            {
                uiDocument.rootVisualElement.style.display = DisplayStyle.None;
            }

            // Wait for end of frame so the UI is fully hidden before capture.
            await UniTask.WaitForEndOfFrame();

            Texture2D tex = null;
            try
            {
                tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                tex.Apply();

                var pngData = tex.EncodeToPNG();

                return new ScreenshotData
                {
                    PngData = pngData,
                    Timestamp = DateTime.Now
                };
            }
            finally
            {
                if (tex != null)
                {
                    UnityEngine.Object.Destroy(tex);
                }

                if (uiDocument != null)
                {
                    uiDocument.rootVisualElement.style.display = DisplayStyle.Flex;
                }
            }
        }
    }
}
#endif
