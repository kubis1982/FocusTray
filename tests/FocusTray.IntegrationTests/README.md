# FocusTray Integration Tests

This project contains integration tests that interact with real external services (JIRA).

## Running JIRA Integration Tests

JIRA integration tests require real credentials. They are **skipped by default** if environment variables are not configured.

### Setup

1. Create a JIRA API token:
   - Go to https://id.atlassian.com/manage-profile/security/api-tokens
   - Click "Create API token"
   - Give it a name (e.g., "FocusTray Integration Tests")
   - Copy the generated token

2. Set environment variables:

**Windows (PowerShell):**
```powershell
$env:JIRA_COMPANY = "yourcompany"  # Company name only (e.g., for yourcompany.atlassian.net)
$env:JIRA_EMAIL = "your.email@company.com"
$env:JIRA_API_TOKEN = "your-api-token-here"
```

**Windows (Command Prompt):**
```cmd
set JIRA_COMPANY=yourcompany
set JIRA_EMAIL=your.email@company.com
set JIRA_API_TOKEN=your-api-token-here
```

**Linux/macOS:**
```bash
export JIRA_COMPANY="yourcompany"
export JIRA_EMAIL="your.email@company.com"
export JIRA_API_TOKEN="your-api-token-here"
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
- Keep your API token secure
