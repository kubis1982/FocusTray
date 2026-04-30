# JIRA Integration Guide

Complete guide for using FocusTray's JIRA integration feature.

## Overview

FocusTray integrates with Atlassian Cloud JIRA to automatically track time spent on tasks. When you link a focus session to a JIRA issue, FocusTray can create worklog entries when the session completes.

## Features

- **Issue Selection**: Browse and select from your assigned JIRA issues
- **Automatic Worklog**: Create worklog entries with one click after session completion
- **Custom JQL Filters**: Configure which issues appear in the selection list
- **Optional Integration**: Works standalone without JIRA - integration is completely optional

## Setup Instructions

### Step 1: Generate JIRA API Token

1. Log in to your Atlassian account
2. Visit [API Token Management](https://id.atlassian.com/manage-profile/security/api-tokens)
3. Click **"Create API token"**
4. Name it descriptively (e.g., "FocusTray Integration")
5. Click **"Create"**
6. **Copy the token immediately** (it won't be shown again)

### Step 2: Configure FocusTray

1. Right-click the FocusTray system tray icon
2. Select **"JIRA Settings"** from the context menu
3. Fill in the configuration form:

   **Company Name**:
   - Your Atlassian company identifier only
   - Format: `yourcompany` (for yourcompany.atlassian.net)
   - Do NOT include `.atlassian.net` or `https://`
   - Example: If your JIRA URL is `https://acmecorp.atlassian.net`, enter `acmecorp`
   
   **Email**:
   - Your Atlassian account email
   - Must match the account that generated the API token
   
   **API Token**:
   - Paste the token you generated in Step 1
   - This is NOT your Atlassian password
   
   **JQL Filter** (Optional):
   - Default: `assignee = currentUser() AND statusCategory != Done`
   - Customize to show specific issues
   - Examples:
     - Only bugs: `assignee = currentUser() AND type = Bug AND status != Done`
     - Specific project: `project = MYPROJECT AND assignee = currentUser()`
     - Recent issues: `assignee = currentUser() AND updated >= -7d ORDER BY updated DESC`

4. Click **"Test Connection"** to verify credentials
5. If successful, click **"Save"**

### Step 3: Using JIRA Integration

#### Starting a Session with JIRA

1. Right-click tray icon → **"Start Focus Session"**
2. Check **"Use JIRA issue"** checkbox
3. Click **"Refresh Issues"** if the list is empty
4. Select an issue from the dropdown
   - Format shown: `PROJ-123: Issue summary`
   - Issues are sorted by most recently updated
5. Set duration (optional - defaults to 25 minutes)
6. Click **"Start"**

#### Starting a Session WITHOUT JIRA

1. Right-click tray icon → **"Start Focus Session"**
2. Leave **"Use JIRA issue"** unchecked
3. Enter a manual task description
4. Click **"Start"**

Both methods work independently - you can mix and match as needed.

#### Logging Time to JIRA

When a JIRA-linked session completes:

1. **Automatic notification** appears
2. Message: "Log 25 minutes to JIRA-123?"
3. Click **"Yes"** to create worklog entry
4. Click **"No"** to skip

**What gets logged:**
- **Time spent**: Session duration (e.g., "25 minutes")
- **Comment**: Session description (e.g., "[PROJ-123] Fix authentication bug")
- **Started**: Timestamp when the session began (auto-calculated)

The worklog appears in JIRA immediately under the issue's "Work log" tab.

## Configuration Details

### Settings File Location

Settings are stored in:
```
%LocalApplicationData%\FocusTray\settings.json
```

Example: `C:\Users\YourName\AppData\Local\FocusTray\settings.json`

### Settings File Format

```json
{
  "JiraConfiguration": {
    "Company": "yourcompany",
    "JqlFilter": "assignee = currentUser() AND statusCategory != Done"
  }
}
```

**Note**: Authentication credentials (email and API token) are stored securely in **Windows Credential Manager**, not in the settings file. This provides better security by using Windows' encrypted credential storage.

### Default JQL Filter Explained

```jql
assignee = currentUser() AND statusCategory != Done ORDER BY updated DESC
```

- `assignee = currentUser()`: Only issues assigned to you
- `statusCategory != Done`: Excludes completed issues
- `ORDER BY updated DESC`: Most recently updated issues first

**Status Categories:**
- `To Do`: Not started
- `In Progress`: Currently being worked on
- `Done`: Completed/resolved/closed

## Troubleshooting

### "Connection Test Failed"

**Possible causes:**
1. **Invalid Base URL**
   - ✅ Correct: `https://yourcompany.atlassian.net`
   - ❌ Wrong: `https://yourcompany.atlassian.net/jira`
   - ❌ Wrong: `yourcompany.atlassian.net` (missing https://)

2. **Invalid API Token**
   - Token may have been revoked
   - Generate a new token and try again

3. **Invalid Email**
   - Email must match the Atlassian account
   - Check for typos

4. **Network/Firewall Issues**
   - Check if you can access JIRA in a browser
   - Corporate firewalls may block API calls

### "No Issues Found"

**Possible causes:**
1. **No Assigned Issues**
   - JQL filter finds no matches
   - Solution: Assign yourself some issues in JIRA

2. **JQL Syntax Error**
   - Check your custom JQL filter
   - Test the JQL in JIRA's issue navigator first
   - Reset to default JQL and try again

3. **Permissions Issue**
   - You may not have permission to view issues
   - Contact your JIRA administrator

### "Failed to Add Worklog"

**Possible causes:**
1. **Issue Closed/Resolved**
   - Some workflows prevent logging time to closed issues
   - Reopen the issue or log time manually

2. **Permissions**
   - You may not have "Work on issues" permission
   - Contact your JIRA administrator

3. **Issue Moved/Deleted**
   - The issue no longer exists
   - Session was started before issue deletion

## Advanced Usage

### Custom JQL Filters

You can customize which issues appear by modifying the JQL filter:

**Show only specific issue types:**
```jql
assignee = currentUser() AND type IN (Bug, Task) AND status != Done
```

**Show issues from specific projects:**
```jql
project IN (PROJ1, PROJ2) AND assignee = currentUser() AND resolution = Unresolved
```

**Show issues with specific labels:**
```jql
assignee = currentUser() AND labels = focus-worthy AND status != Done
```

**Show recently updated issues only:**
```jql
assignee = currentUser() AND updated >= -14d AND status != Done ORDER BY updated DESC
```

### JQL Reference

Common JQL fields:
- `assignee` - Who the issue is assigned to
- `status` - Current workflow status
- `statusCategory` - High-level status (To Do, In Progress, Done)
- `type` - Issue type (Bug, Task, Story, etc.)
- `project` - JIRA project key
- `labels` - Issue labels
- `priority` - Issue priority
- `updated` - Last update timestamp
- `created` - Creation timestamp
- `resolution` - Resolution state

For full JQL documentation, see:
https://support.atlassian.com/jira-service-management-cloud/docs/use-advanced-search-with-jira-query-language-jql/

## Security Best Practices

1. **Never share your API token**
   - Treat it like a password
   - Revoke immediately if compromised

2. **Never commit settings.json to version control**
   - Add to .gitignore if you fork FocusTray
   - Delete from repository history if accidentally committed

3. **Rotate API tokens periodically**
   - Generate new tokens every 90-180 days
   - Revoke old tokens after replacement

4. **Use dedicated API tokens**
   - Create separate tokens for different applications
   - Makes revocation easier if one app is compromised

5. **Monitor token usage**
   - Atlassian shows when tokens were last used
   - Check https://id.atlassian.com/manage-profile/security/api-tokens

## API Endpoints Used

FocusTray uses these JIRA Cloud REST API endpoints:

1. **Connection Test**
   ```
   GET /rest/api/3/myself
   ```
   Returns current user information to verify credentials.

2. **Get Issues**
   ```
   GET /rest/api/3/search?jql={filter}&fields=key,summary,issuetype,status&maxResults=100
   ```
   Fetches issues matching the JQL filter (max 100 results).

3. **Add Worklog**
   ```
   POST /rest/api/3/issue/{issueKey}/worklog
   ```
   Creates a worklog entry with time spent and comment.

**Authentication**: Basic Auth with Base64(`email:apiToken`)

## FAQ

**Q: Does this work with JIRA Server/Data Center?**  
A: No, only Atlassian Cloud is supported. Server/Data Center use different API endpoints.

**Q: Can I use my Atlassian password instead of an API token?**  
A: No, Atlassian Cloud requires API tokens for security. Passwords are not supported.

**Q: How many issues can I select from?**  
A: Up to 100 issues per query. Refine your JQL filter if you have more.

**Q: Can I edit worklogs after creation?**  
A: Not from FocusTray - edit directly in JIRA if needed.

**Q: Does this support multiple JIRA accounts?**  
A: No, only one JIRA account can be configured at a time.

**Q: Can I disable JIRA integration after enabling it?**  
A: Yes, open JIRA Settings and uncheck "Enable JIRA Integration", then save.

**Q: Will my sessions still work if JIRA is down?**  
A: Yes! Sessions work independently. Only the worklog creation will fail (you can log time manually in JIRA later).

## Support

For issues or questions:
- Check this guide first
- Test your JQL in JIRA's issue navigator
- Verify credentials with "Test Connection"
- Open an issue on [GitHub](https://github.com/kubis1982/FocusTray/issues)
