using AwesomeAssertions;
using FocusTray.Core.Models;
using FocusTray.Infrastructure.Jira;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace FocusTray.Tests.Infrastructure;

public class JiraServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<JiraService>> _mockLogger;
    private readonly JiraConfiguration _configuration;
    private readonly JiraService _jiraService;

    public JiraServiceTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _mockLogger = new Mock<ILogger<JiraService>>();
        
        _configuration = new JiraConfiguration
        {
            Enabled = true,
            Company = "test",
            Email = "test@example.com",
            ApiToken = "test-token",
            JqlFilter = "assignee = currentUser() AND statusCategory != Done"
        };

        _jiraService = new JiraService(_httpClient, _configuration, _mockLogger.Object);
    }

    [Fact]
    public void Should_ReturnEnabled_When_ConfigurationIsEnabled()
    {
        // Act
        var isEnabled = _jiraService.IsEnabled;

        // Assert
        isEnabled.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnDisabled_When_ConfigurationIsDisabled()
    {
        // Arrange
        var disabledConfig = new JiraConfiguration { Enabled = false };
        var service = new JiraService(_httpClient, disabledConfig, _mockLogger.Object);

        // Act
        var isEnabled = service.IsEnabled;

        // Assert
        isEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReturnTrue_When_ConnectionTestSucceeds()
    {
        // Arrange
        var responseContent = JsonSerializer.Serialize(new 
        { 
            accountId = "123", 
            emailAddress = "test@example.com",
            displayName = "Test User",
            active = true
        });
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _jiraService.TestConnectionAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnFalse_When_ConnectionTestFails()
    {
        // Arrange
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized
            });

        // Act
        var result = await _jiraService.TestConnectionAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReturnIssues_When_GetAssignedIssuesSucceeds()
    {
        // Arrange
        var searchResponse = new
        {
            issues = new[]
            {
                new
                {
                    key = "PROJ-1",
                    fields = new
                    {
                        summary = "First issue",
                        issuetype = new { name = "Task" },
                        status = new { name = "In Progress" }
                    }
                },
                new
                {
                    key = "PROJ-2",
                    fields = new
                    {
                        summary = "Second issue",
                        issuetype = new { name = "Bug" },
                        status = new { name = "To Do" }
                    }
                }
            }
        };

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/rest/api/2/search/jql")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(searchResponse), System.Text.Encoding.UTF8, "application/json")
            });

        // Act
        var issues = await _jiraService.GetAssignedIssuesAsync();

        // Assert
        issues.Should().HaveCount(2);
        issues[0].Key.Should().Be("PROJ-1");
        issues[0].Summary.Should().Be("First issue");
        issues[1].Key.Should().Be("PROJ-2");
        issues[1].Summary.Should().Be("Second issue");
    }

    [Fact]
    public async Task Should_ReturnEmptyList_When_GetAssignedIssuesFails()
    {
        // Arrange
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Bad Request")
            });

        // Act
        var issues = await _jiraService.GetAssignedIssuesAsync();

        // Assert
        // Service returns empty list on error instead of throwing
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnTrue_When_AddWorklogSucceeds()
    {
        // Arrange
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("/rest/api/2/issue/") &&
                    req.RequestUri!.ToString().Contains("/worklog")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Created,
                Content = new StringContent("{\"id\": \"10000\"}", System.Text.Encoding.UTF8, "application/json")
            });

        var worklog = new JiraWorklog
        {
            IssueKey = "PROJ-1",
            TimeSpentSeconds = 3600, // 60 minutes
            Comment = "Worked on implementation",
            Started = DateTime.UtcNow
        };

        // Act
        var result = await _jiraService.AddWorklogAsync(worklog);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnFalse_When_AddWorklogFails()
    {
        // Arrange
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest
            });

        var worklog = new JiraWorklog
        {
            IssueKey = "PROJ-1",
            TimeSpentSeconds = 3600,
            Comment = "Test",
            Started = DateTime.UtcNow
        };

        // Act
        var result = await _jiraService.AddWorklogAsync(worklog);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_IncludeAuthorizationHeader_When_MakingRequest()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"accountId\": \"123\"}")
            });

        // Act
        await _jiraService.TestConnectionAsync();

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Basic");
    }

    [Fact]
    public async Task Should_UseConfiguredJqlFilter_When_GetAssignedIssues()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri!.ToString().Contains("/rest/api/2/search/jql")),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"issues\": []}", System.Text.Encoding.UTF8, "application/json")
            });

        // Act
        await _jiraService.GetAssignedIssuesAsync();

        // Assert
        capturedRequest.Should().NotBeNull();
        var queryString = capturedRequest!.RequestUri!.Query;
        queryString.Should().Contain("jql="); // JQL parameter present
    }
}
