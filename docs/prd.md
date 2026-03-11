# Product Requirement Document (PRD): FocusTray

| Attribute | Details |
| :--- | :--- |
| **Project Name** | FocusTray |
| **Owner** | Kubis1982
| **Status** | Active / Draft |
| **Target Framework** | .NET 10.0 (Self-contained) |
| **Operating System** | Windows 10/11 |

---

## 1. Project Vision
**FocusTray** is a minimalist utility application residing in the Windows system tray. Its primary purpose is to automate Deep Work sessions by synchronizing a task timer with the user's availability status in Microsoft Teams.

---

## 2. Problem Statement
* **Distractions:** Incoming Teams notifications break the user's flow and concentration.
* **Lack of Communication:** Colleagues are unaware when a user is busy with a specific task, leading to unnecessary interruptions.
* **Manual Overhead:** Manually changing status to "Do Not Disturb" and updating status messages for every study or coding session is tedious and often forgotten.

---

## 3. Technical Specification (.NET 10 Focus)

### 3.1. Architecture and Runtime
* **Framework:** .NET 10.0 (Windows Desktop).
* **Deployment Model:** **Self-contained**. The application bundles all necessary .NET 10 runtime libraries, ensuring:
    * No requirement for the user to install the .NET Desktop Runtime manually.
    * Independence from the framework versions installed on the host system.
    * Simplified updates (everything contained within a single folder/executable).
* **Optimization:**
    * `PublishSingleFile=true`: The entire application is packaged into a single `.exe` file.
    * `PublishReadyToRun=true`: AOT (Ahead-Of-Time) compilation for near-instant startup.

### 3.2. Integrations and Libraries
* **Microsoft Graph API:** For managing presence (`Presence`) and status messages.
* **MSAL.NET:** Secure OAuth2 authentication via Microsoft Entra ID.
* **H.NotifyIcon:** Comprehensive library for handling Windows system tray menus and notifications.

---

## 4. Functional Requirements

### 4.1. User Interface (UX)
* **Tray Icon:** A system tray icon that provides visual feedback on the current state (e.g., distinct icons for "Active" vs. "Idle").
* **Task Configuration:** A popup window containing:
    * `Task Description` (e.g., "Refactoring Module X").
    * `Duration` (input in minutes).
    * `Checkbox: Teams Sync` (toggle for automatic status updates).
* **Notifications:** Windows Toast notifications triggered upon timer completion.

### 4.2. Teams Integration
* **Session Start:** Automatically sets the Teams status to **"Do Not Disturb"** and updates the status message to the task description.
* **Session End:** A notification window appears with two options:
    * **Finish:** Reverts status to "Available" and clears the status message.
    * **Extend (+15 min):** Adds time to the current timer and maintains the DND status.

---

## 5. Distribution and DevOps (Automation)

### 5.1. GitHub Actions (CI/CD)
* **Build:** Automatically triggers a `Self-contained` build for `win-x64` on every merge to `main`.
* **Release:** Automatically creates a GitHub Release when a version tag (e.g., `v1.0.0`) is pushed.

### 5.2. Package Managers
* **Winget:** Automatically generates and submits manifests to the Microsoft Community Repository.
* **Chocolatey (Choco):** Automatically updates the `.nupkg` package and pushes it to the Chocolatey Community Repository.

---

## 6. User Flow
1. **Launch:** The app starts and sits quietly in the system tray.
2. **Setup:** The user clicks the icon -> enters "Learning Rust" -> sets "45 min" -> clicks Start.
3. **Execution:** Teams status automatically changes to DND. FocusTray tracks time in the background.
4. **Completion:** A Windows notification appears -> User clicks "Finish" -> Teams status reverts to "Available".

---

## 7. Roadmap
* **V1.0:** MVP (Timer + Tray Icon + Teams Integration + Self-contained .exe).
* **V1.1:** Full GitHub Actions automation for Winget and Chocolatey.
* **V1.2:** Support for multiple focus profiles (e.g., "Coding", "Meeting", "Deep Work").
* **V2.0:** Multi-platform integrations (Slack and Discord support).