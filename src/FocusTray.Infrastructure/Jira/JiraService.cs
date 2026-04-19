using FocusTray.Core.Models;
using FocusTray.Core.Services;
using Kubis1982.Atlassian.Jira.RestClient.V2;
using Kubis1982.Atlassian.Jira.RestClient.V2.Models;
using Kubis1982.Atlassian.RestClient;
using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions.Serialization;

namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Service for interacting with JIRA Atlassian Cloud via REST API.
/// </summary>
public class JiraService : IJiraService
{
    private readonly JiraRestClient _jiraClient;
    private readonly JiraConfiguration _configuration;
    private readonly ILogger<JiraService> _logger;

    public JiraService(
        HttpClient httpClient,
        JiraConfiguration configuration,
        ILogger<JiraService> logger)
    {
        _ = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var basicAuthProvider = new BasicAuthProvider(_configuration.Email, _configuration.ApiToken);

        _jiraClient = JiraRestClient.Create(_configuration.Company, basicAuthProvider, httpClient); 
    }

    public bool IsEnabled => _configuration.IsValid;

    public async Task<bool> TestConnectionAsync()
    {
        if (!_configuration.IsValid)
        {
            _logger.LogWarning("JIRA configuration is invalid or disabled");
            return false;
        }

        try
        {
            _logger.LogInformation("Testing JIRA connection to {Company}", _configuration.Company);

            var user = await _jiraClient.Rest.Api.Two.Myself.GetAsync();

            if (user != null)
            {
                _logger.LogInformation("JIRA connection test successful");
                return true;
            }

            _logger.LogWarning("JIRA connection test failed: user is null");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing JIRA connection");
            return false;
        }
    }

    public async Task<IReadOnlyList<JiraIssue>> GetAssignedIssuesAsync()
    {
        if (!_configuration.IsValid)
        {
            _logger.LogWarning("JIRA is not configured or disabled");
            return Array.Empty<JiraIssue>();
        }

        try
        {
            _logger.LogInformation("Fetching assigned JIRA issues with JQL: {JQL}", _configuration.JqlFilter);


            var searchResponse = await _jiraClient.Rest.Api.Two.Search.Jql.GetAsync(q =>
            {
                q.QueryParameters.Jql = _configuration.JqlFilter;
                q.QueryParameters.Fields = new[] { "key", "summary", "issuetype", "status" };
                q.QueryParameters.MaxResults = 100;
            });

            if (searchResponse?.Issues == null)
            {
                _logger.LogWarning("No issues returned from JIRA");
                return Array.Empty<JiraIssue>();
            }

            var issues = searchResponse.Issues
                .Select(issue =>
                {
                    var fields = issue.Fields?.AdditionalData;
                    var summary = fields?.TryGetValue("summary", out var summaryObj) == true ? summaryObj?.ToString() : null;
                    var issueType = fields?.TryGetValue("issuetype", out var issueTypeObj) == true && issueTypeObj is IParsable issueTypeParsable
                        ? (issueTypeParsable as IssueTypeDetails)?.Name ?? "Unknown"
                        : "Unknown";
                    var status = fields?.TryGetValue("status", out var statusObj) == true && statusObj is IParsable statusParsable
                        ? (statusParsable as StatusDetails)?.Name ?? "Unknown"
                        : "Unknown";

                    return new JiraIssue
                    {
                        Key = issue.Key!,
                        Summary = summary!,
                        IssueType = issueType,
                        Status = status
                    };
                })
                .ToList();

            _logger.LogInformation("Retrieved {Count} JIRA issues", issues.Count);
            return issues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching JIRA issues");
            return Array.Empty<JiraIssue>();
        }
    }

    public async Task<bool> AddWorklogAsync(JiraWorklog worklog)
    {
        if (!_configuration.IsValid)
        {
            _logger.LogWarning("JIRA is not configured or disabled");
            return false;
        }

        if (string.IsNullOrWhiteSpace(worklog.IssueKey))
        {
            _logger.LogWarning("Cannot add worklog: issue key is empty");
            return false;
        }

        try
        {
            _logger.LogInformation(
                "Adding worklog to {IssueKey}: {Seconds}s on company {Company}",
                worklog.IssueKey,
                worklog.TimeSpentSeconds,
                _configuration.Company);

            var worklogRequest = new Worklog
            {
                TimeSpentSeconds = worklog.TimeSpentSeconds,
                Started = worklog.Started,
                Comment = worklog.Comment
            };

            await _jiraClient.Rest.Api.Two.Issue[worklog.IssueKey].Worklog.PostAsync(worklogRequest);

            _logger.LogInformation("Worklog added successfully to {IssueKey}", worklog.IssueKey);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding worklog to {IssueKey}", worklog.IssueKey);
            return false;
        }
    }
}
