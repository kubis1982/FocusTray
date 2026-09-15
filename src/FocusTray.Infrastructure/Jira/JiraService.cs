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
    private readonly IJiraAuthService _authService;
    private readonly JiraConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraService> _logger;

    public JiraService(
        IJiraAuthService authService,
        JiraConfiguration configuration,
        HttpClient httpClient,
        ILogger<JiraService> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsEnabled => _authService.IsLoggedIn;

    public async Task<bool> TestConnectionAsync()
    {
        if (!_authService.IsLoggedIn)
        {
            _logger.LogWarning("JIRA user is not logged in");
            return false;
        }

        try
        {
            var jiraClient = CreateJiraClient();
            if (jiraClient == null)
            {
                return false;
            }

            _logger.LogInformation("Testing JIRA connection to {Site}", _authService.CurrentSiteUrl);

            var user = await jiraClient.Rest.Api.Two.Myself.GetAsync();

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
        if (!_authService.IsLoggedIn)
        {
            _logger.LogWarning("JIRA user is not logged in");
            return Array.Empty<JiraIssue>();
        }

        try
        {
            var jiraClient = CreateJiraClient();
            if (jiraClient == null)
            {
                return Array.Empty<JiraIssue>();
            }

            _logger.LogInformation("Fetching assigned JIRA issues with JQL: {JQL}", _configuration.JqlFilter);

            var searchResponse = await jiraClient.Rest.Api.Two.Search.Jql.GetAsync(q =>
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
        if (!_authService.IsLoggedIn)
        {
            _logger.LogWarning("JIRA user is not logged in");
            return false;
        }

        if (string.IsNullOrWhiteSpace(worklog.IssueKey))
        {
            _logger.LogWarning("Cannot add worklog: issue key is empty");
            return false;
        }

        try
        {
            var jiraClient = CreateJiraClient();
            if (jiraClient == null)
            {
                return false;
            }

            _logger.LogInformation(
                "Adding worklog to {IssueKey}: {Seconds}s on site {Site}",
                worklog.IssueKey,
                worklog.TimeSpentSeconds,
                _authService.CurrentSiteUrl);

            var worklogRequest = new Worklog
            {
                TimeSpentSeconds = worklog.TimeSpentSeconds,
                Started = worklog.Started,
                Comment = worklog.Comment
            };

            await jiraClient.Rest.Api.Two.Issue[worklog.IssueKey].Worklog.PostAsync(worklogRequest);

            _logger.LogInformation("Worklog added successfully to {IssueKey}", worklog.IssueKey);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding worklog to {IssueKey}", worklog.IssueKey);
            return false;
        }
    }

    private JiraRestClient? CreateJiraClient()
    {
        try
        {
            var cloudId = _authService.CurrentCloudId;
            if (string.IsNullOrWhiteSpace(cloudId))
            {
                _logger.LogWarning("Cannot create JIRA client: not logged in (no cloud ID)");
                return null;
            }

            var baseUrl = JiraOAuthConfiguration.BuildApiBaseUrl(cloudId);
            var authProvider = new JiraBearerAuthProvider(() => _authService.GetAccessTokenAsync());
            var requestAdapter = HttpClientRequestAdapterFactory.Create(baseUrl, authProvider, _httpClient);

            return new JiraRestClient(requestAdapter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating JIRA client");
            return null;
        }
    }
}
