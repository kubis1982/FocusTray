using AwesomeAssertions;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using FocusTray.Infrastructure.Jira;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FocusTray.IntegrationTests.Jira;

/// <summary>
/// Integration tests for JiraService against a real JIRA Cloud site.
/// These tests require a JIRA OAuth2 access token obtained via the real interactive
/// login flow (see docs/JIRA_INTEGRATION.md "Manual OAuth2 test procedure"), exposed via:
/// - JIRA_ACCESS_TOKEN (a currently-valid OAuth2 access token)
/// - JIRA_CLOUD_ID (the Atlassian cloudId for the target site)
///
/// Tests are skipped if environment variables are not set.
/// </summary>
public class JiraServiceIntegrationTests : IDisposable
{
    private readonly JiraService? _jiraService;
    private readonly HttpClient _httpClient;
    private readonly bool _isConfigured;
    private readonly string _skipReason;

    public JiraServiceIntegrationTests()
    {
        _httpClient = new HttpClient();

        var accessToken = Environment.GetEnvironmentVariable("JIRA_ACCESS_TOKEN");
        var cloudId = Environment.GetEnvironmentVariable("JIRA_CLOUD_ID");

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(cloudId))
        {
            _isConfigured = false;
            _skipReason = "JIRA environment variables not configured. Set JIRA_ACCESS_TOKEN and JIRA_CLOUD_ID to run integration tests.";
            return;
        }

        _isConfigured = true;
        _skipReason = string.Empty;

        var authService = new TestJiraAuthService(accessToken, cloudId);

        var configuration = new JiraConfiguration
        {
            JqlFilter = "assignee = currentUser() AND statusCategory != Done ORDER BY updated DESC"
        };

        var logger = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning))
            .CreateLogger<JiraService>();

        _jiraService = new JiraService(authService, configuration, _httpClient, logger);
    }

    [SkippableFact]
    public async Task Should_ConnectToJira_When_AccessTokenIsValid()
    {
        Skip.IfNot(_isConfigured, _skipReason);

        var result = await _jiraService!.TestConnectionAsync();

        result.Should().BeTrue("connection test should succeed with a valid access token");
    }

    [SkippableFact]
    public async Task Should_RetrieveAssignedIssues_When_CallingGetAssignedIssues()
    {
        Skip.IfNot(_isConfigured, _skipReason);

        var issues = await _jiraService!.GetAssignedIssuesAsync();

        issues.Should().NotBeNull("service should return a list");
        if (issues.Count > 0)
        {
            var firstIssue = issues[0];
            firstIssue.Key.Should().NotBeNullOrWhiteSpace("issue key should be populated");
            firstIssue.Summary.Should().NotBeNullOrWhiteSpace("issue summary should be populated");
            firstIssue.Status.Should().NotBeNullOrWhiteSpace("issue status should be populated");
        }
    }

    [SkippableFact]
    public async Task Should_AddWorklog_When_ValidIssueKeyProvided()
    {
        Skip.IfNot(_isConfigured, _skipReason);

        var issues = await _jiraService!.GetAssignedIssuesAsync();

        Skip.If(issues.Count == 0, "No assigned issues available to test worklog creation");

        var testIssue = issues[0];
        var worklog = new JiraWorklog
        {
            IssueKey = testIssue.Key,
            TimeSpentSeconds = 300,
            Comment = "FocusTray Integration Test - Auto-generated worklog entry",
            Started = DateTime.UtcNow.AddMinutes(-5)
        };

        var result = await _jiraService.AddWorklogAsync(worklog);

        result.Should().BeTrue($"worklog should be added successfully to issue {testIssue.Key}");
    }

    [SkippableFact]
    public async Task Should_ReturnFalse_When_AddingWorklogToNonExistentIssue()
    {
        Skip.IfNot(_isConfigured, _skipReason);

        var worklog = new JiraWorklog
        {
            IssueKey = "NONEXISTENT-99999",
            TimeSpentSeconds = 300,
            Comment = "This should fail",
            Started = DateTime.UtcNow
        };

        var result = await _jiraService!.AddWorklogAsync(worklog);

        result.Should().BeFalse("worklog should fail for non-existent issue");
    }

    [SkippableFact]
    public async Task Should_HandleLargeJqlResults_When_ManyIssuesExist()
    {
        Skip.IfNot(_isConfigured, _skipReason);

        var issues = await _jiraService!.GetAssignedIssuesAsync();

        issues.Should().NotBeNull();
        issues.Count.Should().BeLessThanOrEqualTo(100, "service should respect maxResults=100 limit");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    private class TestJiraAuthService(string accessToken, string cloudId) : IJiraAuthService
    {
        public bool IsLoggedIn => true;
        public string? CurrentUsername => "Integration Test User";
        public string? CurrentUserEmail => null;
        public string? CurrentSiteUrl => null;
        public string? CurrentCloudId => cloudId;
        public event EventHandler<AuthStateChangedEventArgs>? AuthStateChanged;

        public Task<bool> LoginAsync() => Task.FromResult(true);
        public Task<bool> LogoutAsync() => Task.FromResult(true);
        public Task<string?> GetCurrentUserAsync() => Task.FromResult<string?>(CurrentUsername);
        public Task<bool> TryAutoLoginAsync() => Task.FromResult(true);
        public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>(accessToken);
    }
}
