using AwesomeAssertions;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
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
    private readonly Mock<IJiraAuthService> _mockAuthService;
    private readonly JiraConfiguration _configuration;
    private readonly JiraService _jiraService;

    public JiraServiceTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _mockLogger = new Mock<ILogger<JiraService>>();
        _mockAuthService = new Mock<IJiraAuthService>();

        _configuration = new JiraConfiguration
        {
            JqlFilter = "assignee = currentUser() AND statusCategory != Done"
        };

        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(true);
        _mockAuthService.Setup(x => x.CurrentCloudId).Returns("test-cloud-id");
        _mockAuthService.Setup(x => x.CurrentUsername).Returns("Test User");
        _mockAuthService.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync("test-access-token");

        _jiraService = new JiraService(_mockAuthService.Object, _configuration, _httpClient, _mockLogger.Object);
    }

    [Fact]
    public void Should_ReturnEnabled_When_UserIsLoggedIn()
    {
        // Arrange
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(true);

        // Act
        var isEnabled = _jiraService.IsEnabled;

        // Assert
        isEnabled.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnDisabled_When_UserIsNotLoggedIn()
    {
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(false);
        var service = new JiraService(_mockAuthService.Object, _configuration, _httpClient, _mockLogger.Object);

        var isEnabled = service.IsEnabled;

        isEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReturnTrue_When_ConnectionTestSucceeds()
    {
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

        var result = await _jiraService.TestConnectionAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnFalse_When_ConnectionTestFails()
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized });

        var result = await _jiraService.TestConnectionAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReturnIssues_When_GetAssignedIssuesSucceeds()
    {
        var searchResponse = new
        {
            issues = new[]
            {
                new { key = "PROJ-1", fields = new { summary = "First issue", issuetype = new { name = "Task" }, status = new { name = "In Progress" } } },
                new { key = "PROJ-2", fields = new { summary = "Second issue", issuetype = new { name = "Bug" }, status = new { name = "To Do" } } }
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

        var issues = await _jiraService.GetAssignedIssuesAsync();

        issues.Should().HaveCount(2);
        issues[0].Key.Should().Be("PROJ-1");
        issues[0].Summary.Should().Be("First issue");
        issues[1].Key.Should().Be("PROJ-2");
        issues[1].Summary.Should().Be("Second issue");
    }

    [Fact]
    public async Task Should_ReturnEmptyList_When_GetAssignedIssuesFails()
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.BadRequest, Content = new StringContent("Bad Request") });

        var issues = await _jiraService.GetAssignedIssuesAsync();

        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnTrue_When_AddWorklogSucceeds()
    {
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

        var worklog = new JiraWorklog { IssueKey = "PROJ-1", TimeSpentSeconds = 3600, Comment = "Worked on implementation", Started = DateTime.UtcNow };

        var result = await _jiraService.AddWorklogAsync(worklog);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnFalse_When_AddWorklogFails()
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.BadRequest });

        var worklog = new JiraWorklog { IssueKey = "PROJ-1", TimeSpentSeconds = 3600, Comment = "Test", Started = DateTime.UtcNow };

        var result = await _jiraService.AddWorklogAsync(worklog);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_IncludeBearerAuthorizationHeader_When_MakingRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{\"accountId\": \"123\"}") });

        await _jiraService.TestConnectionAsync();

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization!.Parameter.Should().Be("test-access-token");
        capturedRequest.RequestUri!.AbsoluteUri.Should().StartWith("https://api.atlassian.com/ex/jira/test-cloud-id/rest/api/2/");
    }

    [Fact]
    public async Task Should_UseConfiguredJqlFilter_When_GetAssignedIssues()
    {
        HttpRequestMessage? capturedRequest = null;
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/rest/api/2/search/jql")),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{\"issues\": []}", System.Text.Encoding.UTF8, "application/json") });

        await _jiraService.GetAssignedIssuesAsync();

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.Query.Should().Contain("jql=");
    }
}
