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

### Step 1: Register a JIRA OAuth 2.0 (3LO) app (one-time, per fork/install)

FocusTray authenticates to JIRA Cloud using OAuth 2.0 (3LO) with PKCE, the same
browser-based sign-in style used for Microsoft Teams. If you're running an
official FocusTray build, this is already configured — skip to Step 2.

If you're building FocusTray from source, you (or your organization) need your
own app registration:

1. Go to the [Atlassian Developer Console](https://developer.atlassian.com/console/myapps)
2. Create a new app → **OAuth 2.0 (3LO)** integration, **public client** (no client secret)
3. Add these API scopes: `read:jira-work`, `write:jira-work`, `read:jira-user`, `offline_access`
4. Set the **Callback URL** to `http://localhost:8082/callback`
5. Copy the **Client ID** and set it as `JiraOAuthConfiguration.ClientId` in
   `src/FocusTray.Infrastructure/Jira/JiraOAuthConfiguration.cs`

### Step 2: Connect FocusTray to JIRA

1. Right-click the FocusTray system tray icon
2. Select **"JIRA Settings"** → **"Login to JIRA"**
3. Click **"Sign in with Atlassian"** — your browser opens Atlassian's login page
4. Log in and approve access
5. If your account has access to more than one JIRA site, choose which one to connect
6. FocusTray shows "Successfully signed in to JIRA as \<your name\>!"

**JQL Filter** (optional, configured separately via **"JIRA Advanced Settings"**):
- Default: `assignee = currentUser() AND statusCategory != Done`
- Customize to show specific issues, e.g.:
  - Only bugs: `assignee = currentUser() AND type = Bug AND status != Done`
  - Specific project: `project = MYPROJECT AND assignee = currentUser()`

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
    "JqlFilter": "assignee = currentUser() AND statusCategory != Done"
  }
}
```

**Note**: OAuth2 tokens (access token, refresh token, expiry, and the connected
JIRA site) are stored encrypted (Windows DPAPI) in
`%LocalAppData%\FocusTray\jira_token_cache.dat`, never in `settings.json`.

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

1. **Tokens are encrypted at rest** using Windows DPAPI, scoped to your Windows
   user account — the same mechanism used for Microsoft Teams tokens.
2. **Refresh tokens rotate**: JIRA issues a new refresh token on every use;
   FocusTray always persists the newest one and discards the old.
3. **Revoke access anytime** from
   [your Atlassian account's connected apps](https://id.atlassian.com/manage-profile/security) —
   FocusTray will require you to sign in again on its next JIRA request.
4. **Never commit `settings.json` or the token cache file to version control.**

## API Endpoints Used

FocusTray uses these JIRA Cloud REST API v2 endpoints, proxied through
Atlassian's OAuth 2.0 (3LO) API gateway (`https://api.atlassian.com/ex/jira/{cloudId}/...`):

1. **Connection Test**: `GET /rest/api/2/myself`
2. **Get Issues**: `GET /rest/api/2/search/jql?jql={filter}&fields=key,summary,issuetype,status&maxResults=100`
3. **Add Worklog**: `POST /rest/api/2/issue/{issueKey}/worklog`

**Authentication**: OAuth 2.0 (3LO), Authorization Code + PKCE, `Authorization: Bearer <access_token>`.

## FAQ

**Q: Does this work with JIRA Server/Data Center?**  
A: No, only Atlassian Cloud is supported. Server/Data Center use different API endpoints.

**Q: Can I use my Atlassian password or an API token instead of signing in with OAuth2?**
A: No. FocusTray only supports OAuth 2.0 sign-in for JIRA now — this is more
secure than API tokens and matches how Microsoft Teams already authenticates.

**Q: How many issues can I select from?**  
A: Up to 100 issues per query. Refine your JQL filter if you have more.

**Q: Can I edit worklogs after creation?**  
A: Not from FocusTray - edit directly in JIRA if needed.

**Q: Does this support multiple JIRA sites?**
A: Yes — if your Atlassian account has access to more than one JIRA Cloud site,
FocusTray asks you to pick one when you sign in. To switch sites later, log out
and log back in.

**Q: Can I disable JIRA integration after enabling it?**  
A: Yes, open JIRA Settings and uncheck "Enable JIRA Integration", then save.

**Q: Will my sessions still work if JIRA is down?**  
A: Yes! Sessions work independently. Only the worklog creation will fail (you can log time manually in JIRA later).

## Manual OAuth2 Test Procedure

The interactive login flow (browser + local loopback listener) can't be
automated in CI, the same way Microsoft Teams' login flow can't. To verify it
manually after changing JIRA auth code:

1. Run FocusTray from source, open **JIRA Settings → Login to JIRA**
2. Click **"Sign in with Atlassian"** — confirm the system browser opens
3. Approve access — confirm the browser tab shows a success page and FocusTray
   shows "Successfully signed in to JIRA as ..."
4. If your account has multiple sites, confirm the site picker appears and the
   chosen site is the one FocusTray actually queries
5. Close FocusTray, reopen it — confirm auto-login succeeds silently (no browser popup)
6. Use **JIRA Advanced Settings → Test Query** to confirm issues load
7. Complete a JIRA-linked focus session and confirm the worklog is created
8. Click **Logout** — confirm `%LocalAppData%\FocusTray\jira_token_cache.dat` is deleted
   and the next JIRA action prompts a fresh login

To populate `JIRA_ACCESS_TOKEN`/`JIRA_CLOUD_ID` for the automated integration
tests (`tests/FocusTray.IntegrationTests`), sign in once via the steps above,
then read the cached access token and cloud ID from
`%LocalAppData%\FocusTray\jira_token_cache.dat` (decrypt with the same DPAPI
call `JiraTokenCacheHelper.Load()` uses) and export them as environment
variables before running `dotnet test tests/FocusTray.IntegrationTests`. Access
tokens are short-lived, so re-export before each run.

## Support

For issues or questions:
- Check this guide first
- Test your JQL in JIRA's issue navigator
- Verify credentials with "Test Connection"
- Open an issue on [GitHub](https://github.com/kubis1982/FocusTray/issues)
