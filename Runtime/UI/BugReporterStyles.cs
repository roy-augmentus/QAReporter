#if UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_QA_REPORTER
using UnityEngine;
using UnityEngine.UIElements;

namespace QAReporter.UI
{
    /// <summary>
    /// Centralized style constants and helper methods for the bug reporter UI.
    /// </summary>
    public static class BugReporterStyles
    {
        // Colors
        public static readonly Color PanelBackground = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        public static readonly Color HeaderBackground = new Color(0.2f, 0.2f, 0.2f, 1f);
        public static readonly Color ButtonNormal = new Color(0.3f, 0.3f, 0.3f, 1f);
        public static readonly Color ButtonPrimary = new Color(0.2f, 0.5f, 0.8f, 1f);
        public static readonly Color ButtonDanger = new Color(0.8f, 0.2f, 0.2f, 1f);
        public static readonly Color RecordingRed = new Color(0.9f, 0.2f, 0.2f, 1f);
        public static readonly Color SuccessGreen = new Color(0.2f, 0.7f, 0.3f, 1f);
        public static readonly Color TextPrimary = new Color(0.9f, 0.9f, 0.9f, 1f);
        public static readonly Color TextSecondary = new Color(0.6f, 0.6f, 0.6f, 1f);
        public static readonly Color InputBackground = new Color(0.12f, 0.12f, 0.12f, 1f);
        public static readonly Color InputFocusedBorder = new Color(0.3f, 0.6f, 0.9f, 1f);
        public static readonly Color BorderColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        // Dimensions
        public const float PanelWidth = 520f;
        public const float PanelMaxHeight = 700f;
        public const float FloatingButtonSize = 40f;
        public const float Padding = 12f;
        public const float SmallPadding = 6f;
        public const float BorderRadius = 6f;
        public const int FontSizeNormal = 13;
        public const int FontSizeSmall = 11;
        public const int FontSizeHeader = 15;
        public const int FontSizeReviewField = 26;
        public const int FontSizeReviewLabel = 22;
        public const int FontSizeReviewPreview = 22;
        public const int FontSizeReview = 20;
        public const int FontSizeReviewSmall = 16;

        public static Button CreateButton(string text, Color bgColor)
        {
            var button = new Button { text = text };
            button.style.backgroundColor = bgColor;
            button.style.color = TextPrimary;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.paddingTop = 6;
            button.style.paddingBottom = 6;
            button.style.paddingLeft = 12;
            button.style.paddingRight = 12;
            button.style.fontSize = FontSizeNormal;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            return button;
        }

        public static Label CreateLabel(string text, int fontSize = FontSizeNormal, Color? color = null)
        {
            var label = new Label(text);
            label.style.fontSize = fontSize;
            label.style.color = color ?? TextPrimary;
            return label;
        }

        public static TextField CreateTextField(string label, bool multiline = false, int maxHeight = 0)
        {
            var field = new TextField(label);
            field.multiline = multiline;
            field.style.marginBottom = SmallPadding;

            // Style the label so it's visible on dark background.
            field.labelElement.style.color = TextPrimary;
            field.labelElement.style.fontSize = FontSizeSmall;
            field.labelElement.style.minWidth = 120;

            // Style the text input area.
            var input = field.Q<VisualElement>("unity-text-input");
            if (input != null)
            {
                input.style.backgroundColor = InputBackground;
                input.style.color = TextPrimary;
                input.style.borderTopWidth = 1;
                input.style.borderBottomWidth = 1;
                input.style.borderLeftWidth = 1;
                input.style.borderRightWidth = 1;
                input.style.borderTopColor = BorderColor;
                input.style.borderBottomColor = BorderColor;
                input.style.borderLeftColor = BorderColor;
                input.style.borderRightColor = BorderColor;
                input.style.borderTopLeftRadius = 4;
                input.style.borderTopRightRadius = 4;
                input.style.borderBottomLeftRadius = 4;
                input.style.borderBottomRightRadius = 4;
                input.style.paddingTop = 4;
                input.style.paddingBottom = 4;
                input.style.paddingLeft = 6;
                input.style.paddingRight = 6;
                input.style.unityFontStyleAndWeight = FontStyle.Normal;

                // Highlight border on focus.
                input.RegisterCallback<FocusInEvent>(_ =>
                {
                    input.style.borderTopColor = InputFocusedBorder;
                    input.style.borderBottomColor = InputFocusedBorder;
                    input.style.borderLeftColor = InputFocusedBorder;
                    input.style.borderRightColor = InputFocusedBorder;
                });
                input.RegisterCallback<FocusOutEvent>(_ =>
                {
                    input.style.borderTopColor = BorderColor;
                    input.style.borderBottomColor = BorderColor;
                    input.style.borderLeftColor = BorderColor;
                    input.style.borderRightColor = BorderColor;
                });
            }

            if (multiline && maxHeight > 0)
            {
                field.style.maxHeight = maxHeight;
            }

            return field;
        }

        public static VisualElement CreateSeparator()
        {
            var sep = new VisualElement();
            sep.style.height = 1;
            sep.style.backgroundColor = BorderColor;
            sep.style.marginTop = SmallPadding;
            sep.style.marginBottom = SmallPadding;
            return sep;
        }
    }
}
#endif
