# In-Game Bug Reporter — Design & Implementation Plan

> **Status:** All code written (Phases 1-6). No prefab needed — UI built in code via UI Toolkit. Runtime testing pending.
> **Branch:** TBD
> **Last Updated:** 2026-03-29

---

## Overview

An in-game bug reporter that captures runtime data (console logs with full stack traces, scene transitions, screenshots) and creates Jira tickets directly from within the Unity app. Designed to produce reports optimized for Claude's `/do-ticket` command to efficiently fix bugs.

## Motivation

- Current bug reports are manually written and lack error logs/stack traces
- SRDebugger's built-in bug reporter sends to Stompy Robot's cloud, not Jira
- Claude needs stack traces + scene context to efficiently locate and fix bugs
- QA testers need a streamlined flow: record bug, fill details, submit ticket

---

## User Flow

```
1. User clicks floating bug icon → opens Bug Reporter panel
2. User clicks [Start Recording]
3. System captures baseline: scene name, timestamp, console log index
4. User reproduces the bug (interacts with app normally)
5. User can click [Screenshot] button to capture screenshots during reproduction
6. User clicks [End Recording]
7. System shows auto-generated preview with:
   - Scene name, recording duration
   - Error logs with full stack traces
   - All console output during recording window
   - Scene transitions detected
8. User fills in:
   - Title (required)
   - Steps to Reproduce (free-text, pre-filled with scene transitions)
   - Expected Behavior (free-text)
   - Actual Behavior (free-text)
   - Test Case ID (optional)
9. Preview text updates live as user edits
10. User clicks [Send] → Jira Bug ticket created in configured project
11. Success screen shows ticket key + URL
```

---

## Jira Ticket Description Format

Optimized for Claude's `/do-ticket` command:

```markdown
## Steps to Reproduce
**Scene:** MeshEditorV2
**Recording:** 2026-03-28 14:32:05 → 14:32:47

1. Open rotary scan project
2. Open autoscan item
3. Change to robot axis and scan type

## Expected Behavior
The fov preview should be in the correct position

## Actual Behavior
The fov preview is rotated 90 degrees.
Also changing max angle doesn't seem to do anything

## Error Logs
[14:32:12] ERROR NullReferenceException: Object reference not set...
  at MyApp.Mesh.Fov.FovVisualizer.UpdateRotation() in Assets/Scripts/Mesh/Fov/FovVisualizer.cs:line 142
  at MyApp.Controller.Autoscan.AutoscanController.OnAxisChanged() in Assets/Scripts/Controller/Autoscan/AutoscanController.cs:line 87

## Console Output
[14:32:05] LOG [Autoscan] Axis changed to Robot
[14:32:06] LOG [Autoscan] ScanType changed to ...
[14:32:08] WARNING [FovVisualizer] Quaternion not normalized
[14:32:12] ERROR NullReferenceException: ...

## Test Case
TC-1234

## System
Unity 6000.0.60f1 | Windows 11 | NVIDIA RTX 4070
```

### Why this format works for `/do-ticket`
- **Scene name at top** — narrows search to specific assemblies
- **Errors first with full stack traces** — Claude jumps straight to the offending file:line
- **Separated errors vs full log** — errors are prominent, full log gives execution context
- **Expected vs Actual** — critical for visual/logic bugs with no error log
- **Test Case ID** — Claude can look up acceptance criteria
- **Recording duration** — helps identify timing/race condition issues

---

## Architecture

### File Structure

```
Assets/Scripts/BugReporter/
├── QAReporter.asmdef
├── BugReporterManager.cs          ← Orchestrator (singleton, DontDestroyOnLoad)
├── BugReporterBootstrapper.cs     ← Auto-init via [RuntimeInitializeOnLoadMethod]
├── Core/
│   ├── BugReportData.cs           ← Data model + markdown generator
│   ├── BugReporterState.cs        ← Enum: Idle/Recording/Review/Sending/Complete/Error
│   ├── LogEntry.cs                ← Log message + enhanced stack trace
│   ├── LogRecorder.cs             ← Captures logs during recording window
│   ├── SceneTransition.cs         ← Scene change record
│   └── ScreenshotData.cs          ← PNG bytes + timestamp
├── Jira/
│   ├── JiraApiClient.cs           ← UnityWebRequest-based Jira REST API client
│   ├── JiraCreateResponse.cs      ← Response model
│   ├── JiraSettings.cs            ← Email/token persisted via PlayerPrefs
│   └── AdfDocumentBuilder.cs      ← Converts report to Jira's ADF JSON format
├── Screenshot/
│   └── BugReporterScreenshotCapturer.cs
└── UI/
    ├── BugReporterUIController.cs ← Builds entire UI in code via UI Toolkit (UIDocument)
    └── BugReporterStyles.cs       ← Centralized style constants and helper methods

Assets/Scripts/Debug/
└── SROptions.BugReporter.cs       ← "Open Bug Reporter" action in SRDebugger

(No prefab needed — UI is built entirely in code via UI Toolkit)
```

### Component Diagram

```
┌──────────────────────────────────────────────────────┐
│                    BugReporterManager                 │
│  (MonoBehaviour, DontDestroyOnLoad, singleton)       │
│                                                       │
│  ┌─────────────┐  ┌──────────────┐  ┌─────────────┐ │
│  │ LogRecorder  │  │ State        │  │ Screenshot  │ │
│  │ (threadsafe) │  │ (ReactiveP.) │  │ Capturer    │ │
│  └──────┬───────┘  └──────┬───────┘  └──────┬──────┘ │
│         │                 │                  │        │
│  ┌──────┴─────────────────┴──────────────────┴─────┐ │
│  │             BugReportData (POCO)                 │ │
│  └──────────────────────┬──────────────────────────┘ │
│                         │                             │
│  ┌──────────────────────┴──────────────────────────┐ │
│  │           JiraApiClient (async UniTask)          │ │
│  └─────────────────────────────────────────────────┘ │
│                                                       │
│  ┌─────────────────────────────────────────────────┐ │
│  │          BugReporterUIController                 │ │
│  │  (Canvas overlay, state-driven panel switching)  │ │
│  └─────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────┘
```

### Data Flow — Recording

```
[Start] → baseline snapshot (scene, time, log index)
    ↓
  Recording... (logs via ConcurrentQueue, scene transitions tracked)
    ↓
[Screenshot] → hide Canvas, WaitForEndOfFrame, ReadPixels, re-show
    ↓
[End] → drain log queue, build BugReportData, auto-generate preview
```

### Data Flow — Submission

```
[Send] → POST /rest/api/3/issue (create Bug ticket)
       → POST /rest/api/3/issue/{key}/attachments (per screenshot)
    ↓
[Complete] → show ticket key + URL
```

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **No Zenject** | Must survive across all scenes. Uses singleton + `DontDestroyOnLoad` like SRDebugger |
| **Own HTTP client** | Jira uses Basic auth (email:token), different from the app's main HTTP client which uses API key headers |
| **Jira REST API v3 with ADF** | v3 requires Atlassian Document Format JSON. Fallback: use v2 API with wiki markup if ADF proves too complex |
| **ConcurrentQueue for logs** | `logMessageReceivedThreaded` fires from any thread |
| **UI Toolkit (not Canvas/uGUI)** | Entire UI built in code — no prefab or UXML needed. Form-heavy UI (text inputs, scrollable preview) is more natural in UI Toolkit. Bug reporter is a dev/QA tool, not customer-facing, so mixing UI systems is fine. PanelSettings sortingOrder=999 for overlay. |
| **Standalone overlay (not inside SRDebugger)** | Recording flow requires app interaction between Start/End; SRDebugger panel would be in the way |
| **Enhanced stack traces** | `System.Diagnostics.StackTrace(true)` captures file paths + line numbers that Unity's default truncates at runtime |

### Enhanced Stack Trace Approach

SRDebugger's stack traces appear truncated for two reasons:
1. **UI preview is capped at 120 chars** (`StackTracePreviewLength` in `IConsoleService.cs`) — but full `StackTrace` string is stored
2. **Unity runtime stack trace setting** (`ProjectSettings.asset` → `m_StackTraceTypes: 01...` = `ScriptOnly`) — only provides method names, no file paths/line numbers

Our solution: register our own `Application.logMessageReceivedThreaded` handler that, for Error/Exception types, also captures `new System.Diagnostics.StackTrace(true)` to get file + line number info without changing global project settings.

**Known limitation:** In IL2CPP/release builds, `StackTrace(true)` may lack file info depending on managed stripping level.

---

## Jira Integration Details

- **Auth:** HTTP Basic (`Authorization: Basic base64(email:token)`)
- **Cloud instance:** `yourcompany.atlassian.net`
- **Default project:** Your Jira project key
- **Issue type:** Bug
- **Endpoints:**
  - Create issue: `POST https://yourcompany.atlassian.net/rest/api/3/issue`
  - Attach file: `POST https://yourcompany.atlassian.net/rest/api/3/issue/{key}/attachments` (header: `X-Atlassian-Token: no-check`)
- **Credentials:** User enters email + API token in settings panel, persisted via `PlayerPrefs`
  - Keys: `BugReporter_JiraEmail`, `BugReporter_JiraApiToken`

---

## UI Panels (UI Toolkit — built in code, no UXML/prefab)

| Panel | State | Contents |
|-------|-------|----------|
| **FloatingButton** | Always visible | Small bug icon, bottom-right. Toggles panel. Pulses red during recording |
| **IdlePanel** | `Idle` | [Start Recording] button, [Settings] button |
| **RecordingPanel** | `Recording` | Pulsing red dot + elapsed time, [Screenshot] button with counter, [End Recording] button |
| **ReviewPanel** | `Review` | Title input, Steps (free-text), Expected/Actual behavior, Test Case ID, live markdown preview, screenshot thumbnails, [Send] / [Cancel] |
| **SendingPanel** | `Sending` | Progress text ("Creating ticket...", "Attaching screenshot 1/3..."), spinner |
| **CompletePanel** | `Complete` | Success message + ticket key, [Open in Browser], [New Report] |
| **SettingsPanel** | (sub-panel) | Jira Email, API Token (masked), Cloud Instance, Project Key, [Test Connection], [Save] / [Cancel] |

---

## Build Sequence (Implementation Order)

### Phase 1: Core Data Models & Log Recorder ✅
- [x] Create `Assets/Scripts/BugReporter/` directory and `QAReporter.asmdef`
- [x] Create `Core/LogEntry.cs`, `Core/SceneTransition.cs`, `Core/ScreenshotData.cs`, `Core/BugReporterState.cs`
- [x] Create `Core/BugReportData.cs` with `GenerateMarkdownDescription()`
- [x] Create `Core/LogRecorder.cs` with enhanced stack trace capture
- [x] **Test:** Unity batch mode compilation — passed (return code 0)

### Phase 2: Jira Integration ✅
- [x] Create `Jira/JiraSettings.cs`
- [x] Create `Jira/AdfDocumentBuilder.cs`
- [x] Create `Jira/JiraCreateResponse.cs`
- [x] Create `Jira/JiraApiClient.cs`
- [ ] **Test:** Create a test Jira ticket via temporary SROptions button (requires runtime testing)

### Phase 3: Screenshot Capturer ✅
- [x] Create `Screenshot/BugReporterScreenshotCapturer.cs`
- [ ] **Test:** Capture + save test screenshot to disk (requires runtime testing)

### Phase 4: Manager & Bootstrapper ✅
- [x] Create `BugReporterManager.cs` (orchestrator with state machine)
- [x] Create `BugReporterBootstrapper.cs` (auto-init via RuntimeInitializeOnLoadMethod)
- [ ] **Test:** End-to-end recording session via code (requires runtime testing)

### Phase 5: UI (UI Toolkit) ✅
- [x] Create `UI/BugReporterStyles.cs` — centralized style constants
- [x] Create `UI/BugReporterUIController.cs` — builds all panels in code (no UXML/prefab)
- [x] Update `BugReporterBootstrapper.cs` — creates UIDocument + PanelSettings at runtime
- [x] Update `Screenshot/BugReporterScreenshotCapturer.cs` — hides UIDocument instead of Canvas
- [x] Removed old Canvas-based UI scripts (8 files → 2 files)
- [x] Removed TMPro asmdef reference (UI Toolkit has own text rendering)
- [ ] **Test:** Full visual flow in editor (requires runtime testing)

### Phase 6: SROptions Integration ✅
- [x] Create `Assets/Scripts/Debug/SROptions.BugReporter.cs`
- [x] Update controller asmdef — added BugReporter GUID reference
- [ ] **Test:** Open bug reporter from SRDebugger options panel (requires runtime testing)

---

## Files to Modify (Existing)

| File | Change |
|------|--------|
| Controller `.asmdef` | ✅ Added GUID `4860635e92e250b4c8a419005b2de2d7` to references |

---

## Limitations & Constraints

- **Jira description ~32KB limit** — truncate console output if exceeded, with note
- **Screenshot memory** — cap at 5 screenshots, downscale if needed
- **IL2CPP builds** — `System.Diagnostics.StackTrace(true)` may lack file info
- **Long recordings** — cap log entries (500 max, keep all errors)
- **ADF complexity** — Jira v3 ADF is verbose; may fall back to v2 wiki markup

---

## Reference: Existing Codebase Patterns

- **SROptions partial classes:** `Assets/Scripts/Debug/SROptions.PathLogic.cs`, `SROptions.AreaSelection.cs` — use `#if !DISABLE_SRDEBUGGER`, `[Category]`, `[DisplayName]`, `FindFirstObjectByType` for scene objects
- **SRDebugger console service:** `Assets/Libraries/StompyRobot/SRDebugger/Scripts/Services/Implementation/StandardConsoleService.cs` — hooks `Application.logMessageReceivedThreaded`, uses `CircularBuffer<ConsoleEntry>`
- **SRDebugger screenshot:** `Assets/Libraries/StompyRobot/SRDebugger/Scripts/Internal/BugReportScreenshotUtil.cs` — WaitForEndOfFrame + ReadPixels + EncodeToPNG
- **HTTP client:** App's main HTTP client — UnityWebRequest wrapper (reference for patterns, not reused directly)
- **Scene detection:** `SceneManager.sceneLoaded` / `SceneManager.activeSceneChanged` events
- **Jira project:** Configured via settings panel
- **Jira fields:** Sprint = `customfield_10020`, Story Points = `customfield_10026`

---

## Development Log

| Date | Phase | What was done |
|------|-------|---------------|
| 2026-03-28 | Planning | Initial design discussion and plan created |
| 2026-03-28 | Phase 1 | Created asmdef, core models (LogEntry, SceneTransition, ScreenshotData, BugReporterState), BugReportData with markdown generator, LogRecorder with enhanced stack traces. Compilation verified. |
| 2026-03-28 | Phase 2 | Created JiraSettings (PlayerPrefs persistence), JiraCreateResponse, AdfDocumentBuilder (full ADF JSON generation), JiraApiClient (create issue + attach screenshots + test connection). Compilation verified. |
| 2026-03-28 | Phase 3 | Created BugReporterScreenshotCapturer (hide canvas → WaitForEndOfFrame → ReadPixels → re-show). Compilation verified. |
| 2026-03-28 | Phase 4 | Created BugReporterManager (singleton orchestrator with ReactiveProperty state machine) and BugReporterBootstrapper (RuntimeInitializeOnLoadMethod auto-init). Compilation verified. |
| 2026-03-28 | Phase 5 | Created all UI scripts: BugReporterUIController, FloatingButton, IdlePanel, RecordingPanel, ReviewPanel (with live preview + UniRx bindings), SendingPanel, CompletePanel, SettingsPanel (with test connection). Added TMPro asmdef reference. Compilation verified. **Prefab must be built manually in Unity Editor.** |
| 2026-03-28 | Phase 6 | Created SROptions.BugReporter.cs, added BugReporter GUID to Controller asmdef. Compilation verified. |
| 2026-03-29 | Phase 5 rewrite | Rewrote UI layer from Canvas/uGUI to UI Toolkit. Replaced 8 MonoBehaviour panel scripts with 2 files (BugReporterUIController + BugReporterStyles). All UI built in code — no prefab/UXML needed. Updated Bootstrapper to create UIDocument + PanelSettings at runtime. Updated ScreenshotCapturer to hide UIDocument. Removed TMPro reference. Compilation verified. |
