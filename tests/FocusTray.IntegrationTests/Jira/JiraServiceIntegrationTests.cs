using AwesomeAssertions;
using FocusTray.Core.Models;
using FocusTray.Infrastructure.Jira;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FocusTray.IntegrationTests.Jira;

/// <summary>
/// Integration tests for JiraService.
/// These tests require real JIRA credentials via environment variables:
/// - JIRA_BASE_URL (e.g., https://yourcompany.atlassian.net)
/// - JIRA_EMAIL (your Atlassian account email)
/// - JIRA_API_TOKEN (API token from https://id.atlassian.com/manage-profile/security/api-tokens)
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
        
        var company = Environment.GetEnvironmentVariable("JIRA_BASE_URL");
        var email = Environment.GetEnvironmentVariable("JIRA_EMAIL");
        var apiToken = Environment.GetEnvironmentVariable("JIRA_API_TOKEN");

        if (string.IsNullOrWhiteSpace(company) || 
            string.IsNullOrWhiteSpace(email) || 
            string.IsNullOrWhiteSpace(apiToken))
        {
            _isConfigured = false;
            _skipReason = "JIRA environment variables not configured. Set JIRA_BASE_URL, JIRA_EMAIL, and JIRA_API_TOKEN to run integration tests.";
            return;
        }

        _isConfigured = true;
        _skipReason = string.Empty;

        var configuration = new JiraConfiguration
        {
            Enabled = true,
            Company = company,
            Email = email,
            ApiToken = apiToken,
            JqlFilter = "assignee = currentUser() AND statusCategory != Done ORDER BY updated DESC"
        };

        var logger = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning))
            .CreateLogger<JiraService>();

        _jiraService = new JiraService(_httpClient, configuration, logger);
    }

    [SkippableFact]
    public async Task Should_ConnectToJira_When_CredentialsAreValid()
    {
        Skip.IfNot(_isConfigured, _skipReason);

        // Act
        var result = await _jiraService!.TestConnectionAsync();

        // Assert
        result.Should().BeTrue("connection test should succeed with valid credentials");
    }

    [SkippableFact]
    public async Task Should_RetrieveAssignedIssues_When_CallingGetAssignedIssues()
    {
        Skip.IfNot(_isConfigured, _skipReason);

        // Act
        var issues = await _jiraService!.GetAssignedIssuesAsync();

        // Assert
        issues.Should().NotBeNull("service should return a list");
        // Note: May be empty if user has no assigned issues
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

        // First, get an issue to add worklog to
        var issues = await _jiraService!.GetAssignedIssuesAsync();
        
        Skip.If(issues.Count == 0, "No assigned issues available to test worklog creation");

        var testIssue = issues[0];
        var worklog = new JiraWorklog
        {
            IssueKey = testIssue.Key,
            TimeSpentSeconds = 300, // 5 minutes
            Comment = "FocusTray Integration Test - Auto-generated worklog entry",
            Started = DateTime.UtcNow.AddMinutes(-5) // Started 5 minutes ago
        };

        // Act
        var result = await _jiraService.AddWorklogAsync(worklog);

        // Assert
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

        // Act
        var result = await _jiraService!.AddWorklogAsync(worklog);

        // Assert
        result.Should().BeFalse("worklog should fail for non-existent issue");
    }

    [SkippableFact]
    public async Task Should_HandleLargeJqlResults_When_ManyIssuesExist()
    {
        Skip.IfNot(_isConfigured, _skipReason);

        // Act
        var issues = await _jiraService!.GetAssignedIssuesAsync();

        // Assert
        issues.Should().NotBeNull();
        issues.Count.Should().BeLessThanOrEqualTo(100, "service should respect maxResults=100 limit");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
