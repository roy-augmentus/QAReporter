# QA Reporter Tool

In-game bug reporter for Unity that captures runtime data and creates Jira tickets directly from within the app.

## Features

- **Recording sessions** — Start/end recording to capture logs during bug reproduction
- **Enhanced stack traces** — `System.Diagnostics.StackTrace(true)` for file paths and line numbers
- **UI interaction tracking** — Automatically captures button clicks, toggles, and other UI interactions via EventSystem
- **Screenshot capture** — Manual screenshot capture with UI hiding
- **Console log attachment** — Full console log attached as `.txt` file to Jira tickets
- **Jira REST API integration** — Creates tickets with description, attachments, and screenshots
- **UI Toolkit overlay** — Entire UI built in code, no prefab required
- **Auto-bootstrap** — Initializes automatically via `[RuntimeInitializeOnLoadMethod]`

## Requirements

- Unity 6 (6000.0+)
- [UniTask](https://github.com/Cysharp/UniTask) (Cysharp.Threading.Tasks)
- [UniRx](https://github.com/neuecc/UniRx) (Reactive Extensions for Unity)
- [Newtonsoft.Json](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html) (included via Unity package)

## Installation

### Via Git URL (Unity Package Manager)

1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL**
3. Enter: `https://github.com/roykoo89/QAReporterTool.git`

### Via local path

1. Clone this repo
2. In Package Manager, click **+** > **Add package from disk**
3. Select the `package.json` in this repo

## Setup

1. Enter Play mode — the floating bug reporter button appears in the bottom-right
2. Click the button > **Settings**
3. Enter your Jira credentials:
   - **Jira Email** — your Atlassian account email
   - **API Token** — generate at https://id.atlassian.com/manage-profile/security/api-tokens
   - **Cloud Instance** — e.g. `yourcompany.atlassian.net`
   - **Project Key** — e.g. `PROJ`
   - **Issue Type** — e.g. `Bug` (must match your Jira project's issue types)
4. Click **Test Connection** to verify, then **Save**

## Usage

1. Click the floating button to open the panel
2. Click **Start Recording**
3. Reproduce the bug (interact with the app normally)
4. Optionally click **Screenshot** to capture screenshots
5. Click **End Recording**
6. Fill in the bug details (title, steps, expected/actual behavior, test case ID)
7. Review the preview
8. Click **Send** — a Jira ticket is created with:
   - Description with error logs and stack traces
   - Console log `.txt` file attachment
   - Screenshot attachments

## Known Limitations

- `System.Diagnostics.StackTrace(true)` may lack file info in IL2CPP builds
- Jira description set via two-step create + PUT (workaround for Jira Cloud template issue)
- PanelSettings sortingOrder must be higher than all uGUI canvases to avoid blurry text

## License

MIT
