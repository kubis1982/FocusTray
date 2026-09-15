# FocusTray Integration Tests

This project contains integration tests that interact with real external services (JIRA).

## Running JIRA Integration Tests

JIRA integration tests require real credentials. They are **skipped by default** if environment variables are not configured.

### Setup

1. Obtain a JIRA OAuth2 access token and cloud ID via a real interactive login:
   - Follow the manual test procedure in `docs/JIRA_INTEGRATION.md` to perform an Authorization Code + PKCE login against Atlassian (this requires an OAuth 2.0 (3LO) app registered in the Atlassian Developer Console — see the main `README.md`)
   - Copy the resulting access token and the JIRA site's cloud ID (the `id` returned from the accessible-resources endpoint)
   - Access tokens are short-lived; repeat the login to get a fresh one if tests start failing with 401s

2. Set environment variables:

**Windows (PowerShell):**
```powershell
$env:JIRA_ACCESS_TOKEN = "your-access-token"
$env:JIRA_CLOUD_ID = "your-cloud-id"
```

**Windows (Command Prompt):**
```cmd
set JIRA_ACCESS_TOKEN=your-access-token
set JIRA_CLOUD_ID=your-cloud-id
```

**Linux/macOS:**
```bash
export JIRA_ACCESS_TOKEN="your-access-token"
export JIRA_CLOUD_ID="your-cloud-id"
```

3. Run tests:
```powershell
dotnet test
```

### Notes

- Tests will be **skipped** if environment variables are not set (no failures)
- The `Should_AddWorklog_When_ValidIssueKeyProvided` test **creates real worklog entries** in your JIRA
- Make sure you have at least one assigned issue for full test coverage
- Tests use the JQL filter: `assignee = currentUser() AND statusCategory != Done ORDER BY updated DESC`

### Security

**⚠️ NEVER commit JIRA credentials to source control!**

- Use environment variables only
- Do not hardcode credentials in test files
- Keep your access token secure
