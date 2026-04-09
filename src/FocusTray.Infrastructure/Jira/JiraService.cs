using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using FocusTray.Infrastructure.Jira.Models;
using Microsoft.Extensions.Logging;

namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Service for interacting with JIRA Atlassian Cloud via REST API.
/// </summary>
public class JiraService : IJiraService
{
    private readonly HttpClient _httpClient;
    private readonly JiraConfiguration _configuration;
    private readonly ILogger<JiraService> _logger;

    public JiraService(
        HttpClient httpClient,
        JiraConfiguration configuration,
        ILogger<JiraService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigureHttpClient();
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
            _logger.LogInformation("Testing JIRA connection to {BaseUrl}", _configuration.BaseUrl);
            
            var response = await _httpClient.GetAsync("/rest/api/3/myself");
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("JIRA connection test successful");
                return true;
            }

            _logger.LogWarning("JIRA connection test failed with status {StatusCode}", response.StatusCode);
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

            var encodedJql = Uri.EscapeDataString(_configuration.JqlFilter);
            var url = $"/rest/api/3/search/jql?jql={encodedJql}&fields=key,summary,issuetype,status&maxResults=100";

            var response = await _httpClient.GetFromJsonAsync<JiraSearchResponse>(url);

            if (response?.Issues == null)
            {
                _logger.LogWarning("No issues returned from JIRA");
                return Array.Empty<JiraIssue>();
            }

            var issues = response.Issues
                .Select(dto => new JiraIssue
                {
                    Key = dto.Key,
                    Summary = dto.Fields.Summary,
                    IssueType = dto.Fields.IssueType?.Name ?? "Unknown",
                    Status = dto.Fields.Status?.Name ?? "Unknown"
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
                "Adding worklog to {IssueKey}: {Seconds}s",
                worklog.IssueKey,
                worklog.TimeSpentSeconds);

            var url = $"/rest/api/3/issue/{worklog.IssueKey}/worklog";
            
            // Format date as: yyyy-MM-dd'T'HH:mm:ss.SSS+0000 (JIRA expects +0000, not +00:00)
            var startedDate = worklog.Started.ToUniversalTime();
            var startedFormatted = startedDate.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "+0000";
            
            var request = new JiraWorklogRequest
            {
                TimeSpentSeconds = worklog.TimeSpentSeconds,
                Comment = new JiraCommentAdf
                {
                    Type = "doc",
                    Version = 1,
                    Content = new List<JiraContentNode>
                    {
                        new JiraContentNode
                        {
                            Type = "paragraph",
                            Content = new List<JiraTextNode>
                            {
                                new JiraTextNode
                                {
                                    Type = "text",
                                    Text = worklog.Comment
                                }
                            }
                        }
                    }
                },
                Started = startedFormatted
            };

            var response = await _httpClient.PostAsJsonAsync(url, request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Worklog added successfully to {IssueKey}", worklog.IssueKey);
                return true;
            }

            // Parse and log detailed error response
            var errorContent = await response.Content.ReadAsStringAsync();
            
            try
            {
                var errorResponse = JsonSerializer.Deserialize<JiraErrorResponse>(errorContent);
                if (errorResponse != null)
                {
                    var formattedError = errorResponse.GetFormattedErrorMessage();
                    _logger.LogWarning(
                        "Failed to add worklog to {IssueKey}. Status: {StatusCode}, Error: {Error}",
                        worklog.IssueKey,
                        response.StatusCode,
                        formattedError);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to add worklog to {IssueKey}. Status: {StatusCode}, Raw response: {Response}",
                        worklog.IssueKey,
                        response.StatusCode,
                        errorContent);
                }
            }
            catch
            {
                // If parsing fails, log raw content
                _logger.LogWarning(
                    "Failed to add worklog to {IssueKey}. Status: {StatusCode}, Raw response: {Response}",
                    worklog.IssueKey,
                    response.StatusCode,
                    errorContent);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding worklog to {IssueKey}", worklog.IssueKey);
            return false;
        }
    }

    private void ConfigureHttpClient()
    {
        if (!_configuration.IsValid)
            return;

        _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_configuration.Email}:{_configuration.ApiToken}"));
        
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", credentials);
        
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
