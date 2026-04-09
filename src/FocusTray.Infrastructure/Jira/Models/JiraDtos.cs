using System.Text.Json.Serialization;

namespace FocusTray.Infrastructure.Jira.Models;

/// <summary>
/// JIRA REST API response for search endpoint.
/// </summary>
internal class JiraSearchResponse
{
    [JsonPropertyName("issues")]
    public List<JiraIssueDto> Issues { get; set; } = new();
    
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// JIRA issue DTO from REST API.
/// </summary>
internal class JiraIssueDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
    
    [JsonPropertyName("fields")]
    public JiraFieldsDto Fields { get; set; } = new();
}

/// <summary>
/// JIRA issue fields DTO.
/// </summary>
internal class JiraFieldsDto
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
    
    [JsonPropertyName("issuetype")]
    public JiraIssueTypeDto? IssueType { get; set; }
    
    [JsonPropertyName("status")]
    public JiraStatusDto? Status { get; set; }
}

/// <summary>
/// JIRA issue type DTO.
/// </summary>
internal class JiraIssueTypeDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// JIRA status DTO.
/// </summary>
internal class JiraStatusDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// JIRA worklog request DTO.
/// </summary>
internal class JiraWorklogRequest
{
    [JsonPropertyName("timeSpentSeconds")]
    public int TimeSpentSeconds { get; set; }
    
    [JsonPropertyName("comment")]
    public JiraCommentAdf Comment { get; set; } = new();
    
    [JsonPropertyName("started")]
    public string Started { get; set; } = string.Empty;
}

/// <summary>
/// JIRA comment in Atlassian Document Format (ADF).
/// </summary>
internal class JiraCommentAdf
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "doc";
    
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;
    
    [JsonPropertyName("content")]
    public List<JiraContentNode> Content { get; set; } = new();
}

/// <summary>
/// Content node in ADF structure.
/// </summary>
internal class JiraContentNode
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("content")]
    public List<JiraTextNode>? Content { get; set; }
}

/// <summary>
/// Text node in ADF structure.
/// </summary>
internal class JiraTextNode
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";
    
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// JIRA error response DTO.
/// </summary>
internal class JiraErrorResponse
{
    /// <summary>
    /// Global/non-field-specific error messages.
    /// </summary>
    [JsonPropertyName("errorMessages")]
    public List<string> ErrorMessages { get; set; } = new();
    
    /// <summary>
    /// Field-specific errors. Key is field name, value is error message.
    /// </summary>
    [JsonPropertyName("errors")]
    public Dictionary<string, string> Errors { get; set; } = new();
    
    /// <summary>
    /// Gets a formatted error message combining all errors.
    /// </summary>
    public string GetFormattedErrorMessage()
    {
        var messages = new List<string>();
        
        if (ErrorMessages.Any())
        {
            messages.AddRange(ErrorMessages);
        }
        
        if (Errors.Any())
        {
            foreach (var error in Errors)
            {
                messages.Add($"{error.Key}: {error.Value}");
            }
        }
        
        return messages.Any() 
            ? string.Join("; ", messages) 
            : "Unknown error occurred";
    }
}
