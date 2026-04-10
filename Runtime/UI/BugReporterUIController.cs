#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using QAReporter.AI;
using QAReporter.Core;
using QAReporter.Jira;
using QAReporter.Slack;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace QAReporter.UI
{
    /// <summary>
    /// Builds and manages the entire bug reporter UI using UI Toolkit.
    /// All UI is constructed in code — no UXML or prefab required.
    /// Attached to the same GameObject as UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class BugReporterUIController : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _mainPanel;
        private VisualElement _floatingButton;
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        // Panel containers — one per state.
        private VisualElement _idlePanel;
        private VisualElement _recordingPanel;
        private VisualElement _reviewPanel;
        private VisualElement _sendingPanel;
        private VisualElement _completePanel;
        private VisualElement _settingsPanel;

        // Sending panel elements.
        private Label _sendingLabel;

        // Recording panel elements.
        private Label _elapsedTimeLabel;
        private Label _screenshotCountLabel;
        private VisualElement _recordingDot;

        // Review panel elements.
        private TextField _titleField;
        private TextField _stepsField;
        private TextField _expectedField;
        private TextField _actualField;
        private TextField _testCaseIdField;
        private Label _previewLabel;
        private ScrollView _previewScroll;
        private Button _sendButton;
        private Button _slackButton;
        private Button _addToTicketButton;
        private TextField _existingTicketKeyField;

        // Complete panel elements.
        private Label _completeMessageLabel;
        private string _ticketUrl;

        // Settings panel elements — Jira.
        private TextField _emailField;
        private TextField _apiTokenField;
        private TextField _cloudInstanceField;
        private TextField _projectKeyField;
        private TextField _issueTypeField;
        private TextField _anthropicApiKeyField;
        private Label _settingsStatusLabel;

        // Settings panel elements — Slack.
        private TextField _slackBotTokenField;
        private TextField _slackChannelIdField;
        private Label _slackSettingsStatusLabel;

        private void Start()
        {
            _uiDocument = GetComponent<UIDocument>();
            _root = _uiDocument.rootVisualElement;

            var manager = BugReporterManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[BugReporter] BugReporterManager not found.");
                return;
            }

            manager.UIDocument = _uiDocument;

            BuildUI();
            BindState(manager);

            _mainPanel.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            var manager = BugReporterManager.Instance;
            if (manager?.State.Value != BugReporterState.Recording || manager.CurrentReport == null)
            {
                return;
            }

            // Update recording panel live elements.
            var elapsed = DateTime.Now - manager.CurrentReport.StartTime;
            if (_elapsedTimeLabel != null)
            {
                _elapsedTimeLabel.text = $"{elapsed:mm\\:ss}";
            }

            if (_screenshotCountLabel != null)
            {
                _screenshotCountLabel.text = $"{manager.CurrentReport.Screenshots.Count}";
            }

            // Pulse recording dot.
            if (_recordingDot != null)
            {
                float alpha = (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f;
                var color = BugReporterStyles.RecordingRed;
                color.a = Mathf.Lerp(0.3f, 1f, alpha);
                _recordingDot.style.backgroundColor = color;
            }
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }

        private void BuildUI()
        {
            _root.style.position = Position.Absolute;
            _root.style.top = 0;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;

            BuildFloatingButton();
            BuildMainPanel();
        }

        // ─── Floating Button ───────────────────────────────────

        private void BuildFloatingButton()
        {
            _floatingButton = new VisualElement();
            _floatingButton.style.position = Position.Absolute;
            _floatingButton.style.bottom = 20;
            _floatingButton.style.right = 20;
            _floatingButton.style.width = BugReporterStyles.FloatingButtonSize;
            _floatingButton.style.height = BugReporterStyles.FloatingButtonSize;
            _floatingButton.style.backgroundColor = BugReporterStyles.ButtonPrimary;
            _floatingButton.style.borderTopLeftRadius = BugReporterStyles.FloatingButtonSize / 2;
            _floatingButton.style.borderTopRightRadius = BugReporterStyles.FloatingButtonSize / 2;
            _floatingButton.style.borderBottomLeftRadius = BugReporterStyles.FloatingButtonSize / 2;
            _floatingButton.style.borderBottomRightRadius = BugReporterStyles.FloatingButtonSize / 2;
            _floatingButton.style.alignItems = Align.Center;
            _floatingButton.style.justifyContent = Justify.Center;
            _floatingButton.pickingMode = PickingMode.Position;

            var icon = BugReporterStyles.CreateLabel("\u2611", 20);
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            _floatingButton.Add(icon);

            _floatingButton.RegisterCallback<ClickEvent>(_ => ToggleMainPanel());
            _root.Add(_floatingButton);
        }

        // ─── Main Panel ────────────────────────────────────────

        private void BuildMainPanel()
        {
            _mainPanel = new VisualElement();
            _mainPanel.style.position = Position.Absolute;
            _mainPanel.style.bottom = 70;
            _mainPanel.style.right = 20;
            _mainPanel.style.width = BugReporterStyles.PanelWidth;
            _mainPanel.style.maxHeight = BugReporterStyles.PanelMaxHeight;
            _mainPanel.style.backgroundColor = BugReporterStyles.PanelBackground;
            _mainPanel.style.borderTopLeftRadius = BugReporterStyles.BorderRadius;
            _mainPanel.style.borderTopRightRadius = BugReporterStyles.BorderRadius;
            _mainPanel.style.borderBottomLeftRadius = BugReporterStyles.BorderRadius;
            _mainPanel.style.borderBottomRightRadius = BugReporterStyles.BorderRadius;
            _mainPanel.style.borderTopWidth = 1;
            _mainPanel.style.borderBottomWidth = 1;
            _mainPanel.style.borderLeftWidth = 1;
            _mainPanel.style.borderRightWidth = 1;
            _mainPanel.style.borderTopColor = BugReporterStyles.BorderColor;
            _mainPanel.style.borderBottomColor = BugReporterStyles.BorderColor;
            _mainPanel.style.borderLeftColor = BugReporterStyles.BorderColor;
            _mainPanel.style.borderRightColor = BugReporterStyles.BorderColor;
            _mainPanel.pickingMode = PickingMode.Position;

            // Header.
            var header = new VisualElement();
            header.style.backgroundColor = BugReporterStyles.HeaderBackground;
            header.style.paddingTop = BugReporterStyles.Padding;
            header.style.paddingBottom = BugReporterStyles.Padding;
            header.style.paddingLeft = BugReporterStyles.Padding;
            header.style.paddingRight = BugReporterStyles.Padding;
            header.style.borderTopLeftRadius = BugReporterStyles.BorderRadius;
            header.style.borderTopRightRadius = BugReporterStyles.BorderRadius;
            header.Add(BugReporterStyles.CreateLabel("Bug Reporter", BugReporterStyles.FontSizeHeader));
            _mainPanel.Add(header);

            // State panels (only one visible at a time).
            _idlePanel = BuildIdlePanel();
            _recordingPanel = BuildRecordingPanel();
            _reviewPanel = BuildReviewPanel();
            _sendingPanel = BuildSendingPanel();
            _completePanel = BuildCompletePanel();
            _settingsPanel = BuildSettingsPanel();

            _mainPanel.Add(_idlePanel);
            _mainPanel.Add(_recordingPanel);
            _mainPanel.Add(_reviewPanel);
            _mainPanel.Add(_sendingPanel);
            _mainPanel.Add(_completePanel);
            _mainPanel.Add(_settingsPanel);

            _root.Add(_mainPanel);
        }

        // ─── Idle Panel ────────────────────────────────────────

        private VisualElement BuildIdlePanel()
        {
            var panel = CreatePanelContainer();

            var startBtn = BugReporterStyles.CreateButton("Start Recording", BugReporterStyles.RecordingRed);
            startBtn.style.height = 40;
            startBtn.clicked += () => BugReporterManager.Instance?.StartRecording();
            panel.Add(startBtn);

            var settingsBtn = BugReporterStyles.CreateButton("Settings", BugReporterStyles.ButtonNormal);
            settingsBtn.style.marginTop = BugReporterStyles.SmallPadding;
            settingsBtn.clicked += () =>
            {
                _idlePanel.style.display = DisplayStyle.None;
                ShowSettingsPanel();
            };
            panel.Add(settingsBtn);

            return panel;
        }

        // ─── Recording Panel ───────────────────────────────────

        private VisualElement BuildRecordingPanel()
        {
            var panel = CreatePanelContainer();

            // Recording indicator row.
            var indicatorRow = new VisualElement();
            indicatorRow.style.flexDirection = FlexDirection.Row;
            indicatorRow.style.alignItems = Align.Center;
            indicatorRow.style.marginBottom = BugReporterStyles.Padding;

            _recordingDot = new VisualElement();
            _recordingDot.style.width = 12;
            _recordingDot.style.height = 12;
            _recordingDot.style.borderTopLeftRadius = 6;
            _recordingDot.style.borderTopRightRadius = 6;
            _recordingDot.style.borderBottomLeftRadius = 6;
            _recordingDot.style.borderBottomRightRadius = 6;
            _recordingDot.style.backgroundColor = BugReporterStyles.RecordingRed;
            _recordingDot.style.marginRight = 8;
            indicatorRow.Add(_recordingDot);

            indicatorRow.Add(BugReporterStyles.CreateLabel("Recording", BugReporterStyles.FontSizeNormal));

            _elapsedTimeLabel = BugReporterStyles.CreateLabel("00:00",
                BugReporterStyles.FontSizeNormal, BugReporterStyles.TextSecondary);
            _elapsedTimeLabel.style.marginLeft = 12;
            indicatorRow.Add(_elapsedTimeLabel);

            panel.Add(indicatorRow);

            // Screenshot button row.
            var screenshotRow = new VisualElement();
            screenshotRow.style.flexDirection = FlexDirection.Row;
            screenshotRow.style.alignItems = Align.Center;
            screenshotRow.style.marginBottom = BugReporterStyles.Padding;

            var screenshotBtn = BugReporterStyles.CreateButton("Screenshot", BugReporterStyles.ButtonNormal);
            screenshotBtn.clicked += () => BugReporterManager.Instance?.CaptureScreenshotAsync().Forget();
            screenshotRow.Add(screenshotBtn);

            _screenshotCountLabel = BugReporterStyles.CreateLabel("0",
                BugReporterStyles.FontSizeSmall, BugReporterStyles.TextSecondary);
            _screenshotCountLabel.style.marginLeft = 8;
            screenshotRow.Add(_screenshotCountLabel);

            panel.Add(screenshotRow);

            // End recording button.
            var endBtn = BugReporterStyles.CreateButton("End Recording", BugReporterStyles.ButtonPrimary);
            endBtn.style.height = 36;
            endBtn.clicked += () => BugReporterManager.Instance?.StopRecording();
            panel.Add(endBtn);

            return panel;
        }

        // ─── Review Panel ──────────────────────────────────────

        private VisualElement BuildReviewPanel()
        {
            var panel = CreatePanelContainer();

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.FlexEnd;

            _titleField = BugReporterStyles.CreateTextField("Title *");
            _titleField.style.fontSize = BugReporterStyles.FontSizeReviewField;
            _titleField.labelElement.style.fontSize = BugReporterStyles.FontSizeReviewLabel;
            _titleField.style.flexGrow = 1;
            _titleField.RegisterValueChangedCallback(_ => OnReviewFieldChanged());
            titleRow.Add(_titleField);

            var suggestBtn = BugReporterStyles.CreateButton("AI", BugReporterStyles.ButtonPrimary);
            suggestBtn.style.height = 28;
            suggestBtn.style.width = 40;
            suggestBtn.style.marginLeft = BugReporterStyles.SmallPadding;
            suggestBtn.style.marginBottom = BugReporterStyles.SmallPadding;
            suggestBtn.clicked += () => SuggestTitleAsync(suggestBtn).Forget();
            titleRow.Add(suggestBtn);

            scroll.Add(titleRow);

            _stepsField = BugReporterStyles.CreateTextField("Steps to Reproduce", true, 200);
            _stepsField.style.fontSize = BugReporterStyles.FontSizeReviewField;
            _stepsField.labelElement.style.fontSize = BugReporterStyles.FontSizeReviewLabel;
            _stepsField.RegisterValueChangedCallback(_ => OnReviewFieldChanged());
            scroll.Add(_stepsField);

            _expectedField = BugReporterStyles.CreateTextField("Expected Behavior", true, 160);
            _expectedField.style.fontSize = BugReporterStyles.FontSizeReviewField;
            _expectedField.labelElement.style.fontSize = BugReporterStyles.FontSizeReviewLabel;
            _expectedField.RegisterValueChangedCallback(_ => OnReviewFieldChanged());
            scroll.Add(_expectedField);

            _actualField = BugReporterStyles.CreateTextField("Actual Behavior", true, 160);
            _actualField.style.fontSize = BugReporterStyles.FontSizeReviewField;
            _actualField.labelElement.style.fontSize = BugReporterStyles.FontSizeReviewLabel;
            _actualField.RegisterValueChangedCallback(_ => OnReviewFieldChanged());
            scroll.Add(_actualField);

            _testCaseIdField = BugReporterStyles.CreateTextField("Test Case ID");
            _testCaseIdField.style.fontSize = BugReporterStyles.FontSizeReviewField;
            _testCaseIdField.labelElement.style.fontSize = BugReporterStyles.FontSizeReviewLabel;
            _testCaseIdField.RegisterValueChangedCallback(_ => OnReviewFieldChanged());
            scroll.Add(_testCaseIdField);

            scroll.Add(BugReporterStyles.CreateSeparator());

            // Preview section.
            scroll.Add(BugReporterStyles.CreateLabel("Preview",
                BugReporterStyles.FontSizeReviewLabel, BugReporterStyles.TextSecondary));

            _previewScroll = new ScrollView(ScrollViewMode.Vertical);
            _previewScroll.style.flexGrow = 1;
            _previewScroll.style.backgroundColor = BugReporterStyles.InputBackground;
            _previewScroll.style.paddingTop = BugReporterStyles.SmallPadding;
            _previewScroll.style.paddingBottom = BugReporterStyles.SmallPadding;
            _previewScroll.style.paddingLeft = BugReporterStyles.SmallPadding;
            _previewScroll.style.paddingRight = BugReporterStyles.SmallPadding;
            _previewScroll.style.borderTopLeftRadius = 4;
            _previewScroll.style.borderTopRightRadius = 4;
            _previewScroll.style.borderBottomLeftRadius = 4;
            _previewScroll.style.borderBottomRightRadius = 4;
            _previewScroll.style.marginTop = 4;

            _previewLabel = new Label();
            _previewLabel.style.fontSize = BugReporterStyles.FontSizeReviewPreview;
            _previewLabel.style.color = BugReporterStyles.TextSecondary;
            _previewLabel.style.whiteSpace = WhiteSpace.Normal;
            _previewScroll.Add(_previewLabel);
            scroll.Add(_previewScroll);

            panel.Add(scroll);

            // Action buttons.
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.marginTop = BugReporterStyles.Padding;

            var cancelBtn = BugReporterStyles.CreateButton("Cancel", BugReporterStyles.ButtonNormal);
            cancelBtn.clicked += () => BugReporterManager.Instance?.Cancel();
            buttonRow.Add(cancelBtn);

            _sendButton = BugReporterStyles.CreateButton("Create Ticket", BugReporterStyles.ButtonPrimary);
            _sendButton.style.marginLeft = BugReporterStyles.SmallPadding;
            _sendButton.SetEnabled(false);
            _sendButton.clicked += () =>
            {
                _sendingLabel.text = "Creating Jira ticket...";
                SubmitAsync().Forget();
            };
            buttonRow.Add(_sendButton);

            _slackButton = BugReporterStyles.CreateButton("Send to Slack", BugReporterStyles.SlackPurple);
            _slackButton.style.marginLeft = BugReporterStyles.SmallPadding;
            _slackButton.SetEnabled(false);
            _slackButton.clicked += () =>
            {
                _sendingLabel.text = "Sending to Slack...";
                SubmitToSlackAsync().Forget();
            };
            buttonRow.Add(_slackButton);

            panel.Add(buttonRow);

            // Add to existing ticket section.
            panel.Add(BugReporterStyles.CreateSeparator());

            _existingTicketKeyField = BugReporterStyles.CreateTextField("Ticket Key");
            _existingTicketKeyField.style.fontSize = BugReporterStyles.FontSizeReviewField;
            _existingTicketKeyField.labelElement.style.fontSize = BugReporterStyles.FontSizeReviewLabel;
            _existingTicketKeyField.RegisterValueChangedCallback(_ =>
                _addToTicketButton.SetEnabled(!string.IsNullOrWhiteSpace(_existingTicketKeyField.value)));
            panel.Add(_existingTicketKeyField);

            _addToTicketButton = BugReporterStyles.CreateButton("Add to Existing Ticket", BugReporterStyles.ButtonPrimary);
            _addToTicketButton.SetEnabled(false);
            _addToTicketButton.clicked += () =>
            {
                _sendingLabel.text = "Adding comment...";
                SubmitAsCommentAsync().Forget();
            };
            panel.Add(_addToTicketButton);

            return panel;
        }

        private void OnReviewFieldChanged()
        {
            var report = BugReporterManager.Instance?.CurrentReport;
            if (report == null)
            {
                return;
            }

            report.Title = _titleField.value;
            report.StepsToReproduce = _stepsField.value;
            report.ExpectedBehavior = _expectedField.value;
            report.ActualBehavior = _actualField.value;
            report.TestCaseId = _testCaseIdField.value;

            _previewLabel.text = report.GenerateMarkdownDescription();

            bool hasTitle = !string.IsNullOrWhiteSpace(_titleField.value);
            _sendButton.SetEnabled(hasTitle);
            _slackButton.SetEnabled(hasTitle);
        }

        private void PopulateReviewPanel()
        {
            var report = BugReporterManager.Instance?.CurrentReport;
            if (report == null)
            {
                return;
            }

            _titleField.value = report.Title ?? "";

            // Pre-fill steps with auto-detected scene transitions.
            if (report.SceneTransitions.Count > 0 && string.IsNullOrEmpty(report.StepsToReproduce))
            {
                var autoSteps = "";
                foreach (var t in report.SceneTransitions)
                {
                    autoSteps += $"[{t.Timestamp:HH:mm:ss}] Scene: {t.FromScene} -> {t.ToScene}\n";
                }
                _stepsField.value = autoSteps;
            }
            else
            {
                _stepsField.value = report.StepsToReproduce ?? "";
            }

            _expectedField.value = report.ExpectedBehavior ?? "";
            _actualField.value = report.ActualBehavior ?? "";
            _testCaseIdField.value = report.TestCaseId ?? "";

            OnReviewFieldChanged();
        }

        // ─── Sending Panel ─────────────────────────────────────

        private VisualElement BuildSendingPanel()
        {
            var panel = CreatePanelContainer();
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.Center;
            panel.style.minHeight = 80;

            _sendingLabel = BugReporterStyles.CreateLabel("Creating Jira ticket...");
            panel.Add(_sendingLabel);
            return panel;
        }

        // ─── Complete Panel ────────────────────────────────────

        private VisualElement BuildCompletePanel()
        {
            var panel = CreatePanelContainer();

            _completeMessageLabel = BugReporterStyles.CreateLabel("",
                BugReporterStyles.FontSizeNormal, BugReporterStyles.SuccessGreen);
            _completeMessageLabel.style.marginBottom = BugReporterStyles.Padding;
            panel.Add(_completeMessageLabel);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;

            var openBtn = BugReporterStyles.CreateButton("Open in Browser", BugReporterStyles.ButtonPrimary);
            openBtn.clicked += () =>
            {
                if (!string.IsNullOrEmpty(_ticketUrl))
                {
                    Application.OpenURL(_ticketUrl);
                }
            };
            buttonRow.Add(openBtn);

            var newBtn = BugReporterStyles.CreateButton("New Report", BugReporterStyles.ButtonNormal);
            newBtn.style.marginLeft = BugReporterStyles.SmallPadding;
            newBtn.clicked += () => BugReporterManager.Instance?.Reset();
            buttonRow.Add(newBtn);

            panel.Add(buttonRow);
            return panel;
        }

        // ─── Settings Panel ────────────────────────────────────

        private VisualElement BuildSettingsPanel()
        {
            var panel = CreatePanelContainer();

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;

            scroll.Add(BugReporterStyles.CreateLabel("Jira Settings",
                BugReporterStyles.FontSizeHeader));
            scroll.Add(BugReporterStyles.CreateSeparator());

            _emailField = BugReporterStyles.CreateTextField("Jira Email");
            scroll.Add(_emailField);

            _apiTokenField = BugReporterStyles.CreateTextField("API Token");
            _apiTokenField.isPasswordField = true;
            scroll.Add(_apiTokenField);

            _cloudInstanceField = BugReporterStyles.CreateTextField("Cloud Instance");
            scroll.Add(_cloudInstanceField);

            _projectKeyField = BugReporterStyles.CreateTextField("Project Key");
            scroll.Add(_projectKeyField);

            _issueTypeField = BugReporterStyles.CreateTextField("Issue Type");
            scroll.Add(_issueTypeField);

            _settingsStatusLabel = BugReporterStyles.CreateLabel("",
                BugReporterStyles.FontSizeSmall, BugReporterStyles.TextSecondary);
            _settingsStatusLabel.style.marginTop = BugReporterStyles.SmallPadding;
            scroll.Add(_settingsStatusLabel);

            var jiraButtonRow = new VisualElement();
            jiraButtonRow.style.flexDirection = FlexDirection.Row;
            jiraButtonRow.style.marginTop = BugReporterStyles.SmallPadding;

            var testBtn = BugReporterStyles.CreateButton("Test Jira", BugReporterStyles.ButtonNormal);
            testBtn.clicked += () => TestConnectionAsync(testBtn).Forget();
            jiraButtonRow.Add(testBtn);
            scroll.Add(jiraButtonRow);

            // Slack settings section.
            scroll.Add(BugReporterStyles.CreateSeparator());
            scroll.Add(BugReporterStyles.CreateLabel("Slack Settings",
                BugReporterStyles.FontSizeHeader));
            scroll.Add(BugReporterStyles.CreateSeparator());

            _slackBotTokenField = BugReporterStyles.CreateTextField("Bot Token");
            _slackBotTokenField.isPasswordField = true;
            scroll.Add(_slackBotTokenField);

            _slackChannelIdField = BugReporterStyles.CreateTextField("Channel ID");
            scroll.Add(_slackChannelIdField);

            _slackSettingsStatusLabel = BugReporterStyles.CreateLabel("",
                BugReporterStyles.FontSizeSmall, BugReporterStyles.TextSecondary);
            _slackSettingsStatusLabel.style.marginTop = BugReporterStyles.SmallPadding;
            scroll.Add(_slackSettingsStatusLabel);

            var slackButtonRow = new VisualElement();
            slackButtonRow.style.flexDirection = FlexDirection.Row;
            slackButtonRow.style.marginTop = BugReporterStyles.SmallPadding;

            var testSlackBtn = BugReporterStyles.CreateButton("Test Slack", BugReporterStyles.SlackPurple);
            testSlackBtn.clicked += () => TestSlackConnectionAsync(testSlackBtn).Forget();
            slackButtonRow.Add(testSlackBtn);
            scroll.Add(slackButtonRow);

            // AI settings section.
            scroll.Add(BugReporterStyles.CreateSeparator());
            scroll.Add(BugReporterStyles.CreateLabel("AI Settings",
                BugReporterStyles.FontSizeHeader));
            scroll.Add(BugReporterStyles.CreateSeparator());

            _anthropicApiKeyField = BugReporterStyles.CreateTextField("Anthropic API Key");
            _anthropicApiKeyField.isPasswordField = true;
            scroll.Add(_anthropicApiKeyField);

            panel.Add(scroll);

            // Bottom action buttons.
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.marginTop = BugReporterStyles.Padding;

            var saveBtn = BugReporterStyles.CreateButton("Save", BugReporterStyles.ButtonPrimary);
            saveBtn.clicked += SaveSettings;
            buttonRow.Add(saveBtn);

            var cancelBtn = BugReporterStyles.CreateButton("Cancel", BugReporterStyles.ButtonNormal);
            cancelBtn.style.marginLeft = BugReporterStyles.SmallPadding;
            cancelBtn.clicked += HideSettingsPanel;
            buttonRow.Add(cancelBtn);

            panel.Add(buttonRow);
            return panel;
        }

        private void ShowSettingsPanel()
        {
            var jiraSettings = JiraSettings.Load();
            _emailField.value = jiraSettings.Email;
            _apiTokenField.value = jiraSettings.ApiToken;
            _cloudInstanceField.value = jiraSettings.CloudInstance;
            _projectKeyField.value = jiraSettings.ProjectKey;
            _issueTypeField.value = jiraSettings.IssueType;
            _anthropicApiKeyField.value = jiraSettings.AnthropicApiKey;
            _settingsStatusLabel.text = "";

            var slackSettings = SlackSettings.Load();
            _slackBotTokenField.value = slackSettings.BotToken;
            _slackChannelIdField.value = slackSettings.ChannelId;
            _slackSettingsStatusLabel.text = "";

            _settingsPanel.style.display = DisplayStyle.Flex;
        }

        private void HideSettingsPanel()
        {
            _settingsPanel.style.display = DisplayStyle.None;
            _idlePanel.style.display = DisplayStyle.Flex;
        }

        private void SaveSettings()
        {
            var jiraSettings = new JiraSettings
            {
                Email = _emailField.value.Trim(),
                ApiToken = _apiTokenField.value.Trim(),
                CloudInstance = _cloudInstanceField.value.Trim(),
                ProjectKey = _projectKeyField.value.Trim(),
                IssueType = _issueTypeField.value.Trim(),
                AnthropicApiKey = _anthropicApiKeyField.value.Trim()
            };
            jiraSettings.Save();

            var slackSettings = new SlackSettings
            {
                BotToken = _slackBotTokenField.value.Trim(),
                ChannelId = _slackChannelIdField.value.Trim()
            };
            slackSettings.Save();

            _settingsStatusLabel.text = "Saved.";
            HideSettingsPanel();
        }

        private async UniTaskVoid TestConnectionAsync(Button testBtn)
        {
            _settingsStatusLabel.text = "Testing Jira...";
            testBtn.SetEnabled(false);

            var settings = new JiraSettings
            {
                Email = _emailField.value.Trim(),
                ApiToken = _apiTokenField.value.Trim(),
                CloudInstance = _cloudInstanceField.value.Trim(),
                ProjectKey = _projectKeyField.value.Trim(),
                IssueType = _issueTypeField.value.Trim(),
                AnthropicApiKey = _anthropicApiKeyField.value.Trim()
            };

            var client = new JiraApiClient(settings);
            var (success, error) = await client.TestConnectionAsync();

            testBtn.SetEnabled(true);
            _settingsStatusLabel.text = success ? "Jira connected!" : $"Failed: {error}";
            _settingsStatusLabel.style.color = success
                ? BugReporterStyles.SuccessGreen
                : BugReporterStyles.RecordingRed;
        }

        private async UniTaskVoid TestSlackConnectionAsync(Button testBtn)
        {
            _slackSettingsStatusLabel.text = "Testing Slack...";
            testBtn.SetEnabled(false);

            var settings = new SlackSettings
            {
                BotToken = _slackBotTokenField.value.Trim(),
                ChannelId = _slackChannelIdField.value.Trim()
            };

            var client = new SlackApiClient(settings);
            var (success, error) = await client.TestConnectionAsync();

            testBtn.SetEnabled(true);
            _slackSettingsStatusLabel.text = success ? "Slack connected!" : $"Failed: {error}";
            _slackSettingsStatusLabel.style.color = success
                ? BugReporterStyles.SuccessGreen
                : BugReporterStyles.RecordingRed;
        }

        // ─── State Management ──────────────────────────────────

        private void BindState(BugReporterManager manager)
        {
            manager.State
                .Subscribe(state =>
                {
                    _idlePanel.style.display = state == BugReporterState.Idle ? DisplayStyle.Flex : DisplayStyle.None;
                    _recordingPanel.style.display = state == BugReporterState.Recording ? DisplayStyle.Flex : DisplayStyle.None;
                    _reviewPanel.style.display = state == BugReporterState.Review ? DisplayStyle.Flex : DisplayStyle.None;
                    _sendingPanel.style.display = state == BugReporterState.Sending ? DisplayStyle.Flex : DisplayStyle.None;
                    _completePanel.style.display =
                        (state == BugReporterState.Complete || state == BugReporterState.Error)
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                    _settingsPanel.style.display = DisplayStyle.None;

                    if (state == BugReporterState.Recording)
                    {
                        _mainPanel.style.display = DisplayStyle.Flex;
                    }

                    // Go full screen for Review/Sending/Complete, collapse for others.
                    bool fullScreen = state == BugReporterState.Review
                        || state == BugReporterState.Sending
                        || state == BugReporterState.Complete
                        || state == BugReporterState.Error;
                    SetPanelFullScreen(fullScreen);

                    if (state == BugReporterState.Review)
                    {
                        PopulateReviewPanel();
                    }
                })
                .AddTo(_disposables);
        }

        private async UniTaskVoid SubmitAsync()
        {
            var manager = BugReporterManager.Instance;
            if (manager == null)
            {
                return;
            }

            var (success, ticketKey, ticketUrl, error) = await manager.SubmitReportAsync();

            if (success)
            {
                _ticketUrl = ticketUrl;
                _completeMessageLabel.text = $"{ticketKey} created!";
                _completeMessageLabel.style.color = BugReporterStyles.SuccessGreen;
            }
            else
            {
                _ticketUrl = null;
                _completeMessageLabel.text = $"Error: {error}";
                _completeMessageLabel.style.color = BugReporterStyles.RecordingRed;
            }
        }

        private async UniTaskVoid SuggestTitleAsync(Button suggestBtn)
        {
            var settings = JiraSettings.Load();
            if (string.IsNullOrWhiteSpace(settings.AnthropicApiKey))
            {
                _titleField.value = "(Set Anthropic API Key in Settings first)";
                return;
            }

            var expected = _expectedField.value;
            var actual = _actualField.value;
            if (string.IsNullOrWhiteSpace(expected) && string.IsNullOrWhiteSpace(actual))
            {
                _titleField.value = "(Fill in Expected/Actual Behavior first)";
                return;
            }

            suggestBtn.SetEnabled(false);
            suggestBtn.text = "...";

            var client = new ClaudeApiClient(settings.AnthropicApiKey);
            var (title, error) = await client.SuggestTitleAsync(expected, actual);

            suggestBtn.SetEnabled(true);
            suggestBtn.text = "AI";

            if (title != null)
            {
                _titleField.value = title;
            }
            else
            {
                Debug.LogError($"[BugReporter] Title suggestion failed: {error}");
            }
        }

        private async UniTaskVoid SubmitAsCommentAsync()
        {
            var manager = BugReporterManager.Instance;
            if (manager == null)
            {
                return;
            }

            var issueKey = _existingTicketKeyField.value.Trim();
            var (success, ticketUrl, error) = await manager.SubmitAsCommentAsync(issueKey);

            if (success)
            {
                _ticketUrl = ticketUrl;
                _completeMessageLabel.text = $"Comment added to {issueKey}!";
                _completeMessageLabel.style.color = BugReporterStyles.SuccessGreen;
            }
            else
            {
                _ticketUrl = null;
                _completeMessageLabel.text = $"Error: {error}";
                _completeMessageLabel.style.color = BugReporterStyles.RecordingRed;
            }
        }

        private async UniTaskVoid SubmitToSlackAsync()
        {
            var manager = BugReporterManager.Instance;
            if (manager == null)
            {
                return;
            }

            var (success, error) = await manager.SubmitToSlackAsync();

            if (success)
            {
                _ticketUrl = null;
                _completeMessageLabel.text = "Bug report sent to Slack!";
                _completeMessageLabel.style.color = BugReporterStyles.SuccessGreen;
            }
            else
            {
                _ticketUrl = null;
                _completeMessageLabel.text = $"Error: {error}";
                _completeMessageLabel.style.color = BugReporterStyles.RecordingRed;
            }
        }

        // ─── Helpers ───────────────────────────────────────────

        private void SetPanelFullScreen(bool fullScreen)
        {
            if (fullScreen)
            {
                _mainPanel.style.top = 20;
                _mainPanel.style.left = 20;
                _mainPanel.style.right = 20;
                _mainPanel.style.bottom = 20;
                _mainPanel.style.width = StyleKeyword.Auto;
                _mainPanel.style.maxHeight = StyleKeyword.None;
            }
            else
            {
                _mainPanel.style.top = StyleKeyword.Auto;
                _mainPanel.style.left = StyleKeyword.Auto;
                _mainPanel.style.right = 20;
                _mainPanel.style.bottom = 70;
                _mainPanel.style.width = BugReporterStyles.PanelWidth;
                _mainPanel.style.maxHeight = BugReporterStyles.PanelMaxHeight;
            }
        }

        private void ToggleMainPanel()
        {
            bool isVisible = _mainPanel.style.display == DisplayStyle.Flex;
            _mainPanel.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>
        /// Shows the main panel (called from SROptions).
        /// </summary>
        public void Show()
        {
            _mainPanel.style.display = DisplayStyle.Flex;
        }

        private static VisualElement CreatePanelContainer()
        {
            var container = new VisualElement();
            container.style.paddingTop = BugReporterStyles.Padding;
            container.style.paddingBottom = BugReporterStyles.Padding;
            container.style.paddingLeft = BugReporterStyles.Padding;
            container.style.paddingRight = BugReporterStyles.Padding;
            return container;
        }
    }
}
#endif
