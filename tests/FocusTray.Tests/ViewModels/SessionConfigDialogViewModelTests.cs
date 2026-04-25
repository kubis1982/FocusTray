using AwesomeAssertions;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using FocusTray.ViewModels;
using Moq;
using Xunit;

namespace FocusTray.Tests.ViewModels;

public class SessionConfigDialogViewModelTests
{
    private readonly Mock<IJiraService> _mockJiraService;
    private readonly Mock<IJiraAuthService> _mockAuthService;
    private readonly SessionConfigDialogViewModel _viewModel;

    public SessionConfigDialogViewModelTests()
    {
        _mockJiraService = new Mock<IJiraService>();
        _mockAuthService = new Mock<IJiraAuthService>();
        _viewModel = new SessionConfigDialogViewModel(_mockJiraService.Object, _mockAuthService.Object);
    }

    [Fact]
    public void Should_InitializeWithDefaultValues_When_Constructed()
    {
        // Assert
        _viewModel.DurationMinutes.Should().Be(25);
        _viewModel.TaskDescription.Should().BeEmpty();
        _viewModel.SelectedIssue.Should().BeNull();
        _viewModel.JiraIssues.Should().BeEmpty();
    }

    [Fact]
    public void Should_HideJiraControls_When_JiraIsDisabled()
    {
        // Arrange
        _mockJiraService.Setup(x => x.IsEnabled).Returns(false);

        // Act
        var isJiraEnabled = _mockJiraService.Object.IsEnabled;

        // Assert
        isJiraEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Should_LoadJiraIssues_When_CommandExecuted()
    {
        // Arrange
        var issues = new List<JiraIssue>
        {
            new() { Key = "PROJ-1", Summary = "First issue", Status = "In Progress" },
            new() { Key = "PROJ-2", Summary = "Second issue", Status = "To Do" }
        };
        _mockJiraService.Setup(x => x.IsEnabled).Returns(true);
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(true);
        _mockJiraService.Setup(x => x.GetAssignedIssuesAsync())
            .ReturnsAsync(issues);

        // Act
        await _viewModel.LoadJiraIssuesCommand.ExecuteAsync(null);

        // Assert
        _viewModel.JiraIssues.Should().HaveCount(2);
        _viewModel.JiraIssues[0].Key.Should().Be("PROJ-1");
        _viewModel.JiraIssues[1].Key.Should().Be("PROJ-2");
    }

    [Fact]
    public async Task Should_HandleError_When_LoadingJiraIssuesFails()
    {
        // Arrange
        _mockJiraService.Setup(x => x.IsEnabled).Returns(true);
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(true);
        _mockJiraService.Setup(x => x.GetAssignedIssuesAsync())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        await _viewModel.LoadJiraIssuesCommand.ExecuteAsync(null);

        // Assert
        _viewModel.JiraIssues.Should().BeEmpty();
        _viewModel.ErrorMessage.Should().Contain("Network error");
    }

    [Fact]
    public void Should_ReturnJiraIssueKey_When_IssueIsSelectedAndLoggedIn()
    {
        // Arrange
        var issue = new JiraIssue { Key = "PROJ-123", Summary = "Test issue", Status = "In Progress" };
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(true);
        _viewModel.SelectedIssue = issue;

        // Act
        var issueKey = _viewModel.GetSelectedIssueKey();

        // Assert
        issueKey.Should().Be("PROJ-123");
    }

    [Fact]
    public void Should_ReturnNull_When_NotLoggedIn()
    {
        // Arrange
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(false);

        // Act
        var issueKey = _viewModel.GetSelectedIssueKey();

        // Assert
        issueKey.Should().BeNull();
    }

    [Fact]
    public void Should_ReturnJiraDescription_When_IssueIsSelectedAndLoggedIn()
    {
        // Arrange
        var issue = new JiraIssue { Key = "PROJ-123", Summary = "Implement feature X", Status = "In Progress" };
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(true);
        _viewModel.SelectedIssue = issue;

        // Act
        var description = _viewModel.GetEffectiveTaskDescription();

        // Assert
        // Uses DisplayText format: "Key: Summary"
        description.Should().Be("PROJ-123: Implement feature X");
    }

    [Fact]
    public void Should_ReturnManualDescription_When_JiraNotUsed()
    {
        // Arrange
        _viewModel.TaskDescription = "Manual task description";
        _viewModel.UseJiraIssue = false;

        // Act
        var description = _viewModel.GetEffectiveTaskDescription();

        // Assert
        description.Should().Be("Manual task description");
    }

    [Fact]
    public void Should_ReturnError_When_LoggedInButNoIssueSelected()
    {
        // Arrange
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(true);
        _viewModel.SelectedIssue = null;

        // Act
        var error = _viewModel.ValidateInput();

        // Assert
        error.Should().NotBeNull();
        error.Should().Contain("select a JIRA issue");
    }

    [Fact]
    public void Should_ReturnError_When_NotLoggedInAndNoDescriptionProvided()
    {
        // Arrange
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(false);
        _viewModel.TaskDescription = "";

        // Act
        var error = _viewModel.ValidateInput();

        // Assert
        error.Should().NotBeNull();
        error.Should().Contain("task description");
    }

    [Fact]
    public void Should_ReturnError_When_DurationIsZero()
    {
        // Arrange
        _viewModel.TaskDescription = "Valid task";
        _viewModel.DurationMinutes = 0;

        // Act
        var error = _viewModel.ValidateInput();

        // Assert
        error.Should().NotBeNull();
        error.Should().Contain("greater than 0");
    }

    [Fact]
    public void Should_ReturnError_When_DurationExceeds24Hours()
    {
        // Arrange
        _viewModel.TaskDescription = "Valid task";
        _viewModel.DurationMinutes = 1500; // 25 hours

        // Act
        var error = _viewModel.ValidateInput();

        // Assert
        error.Should().NotBeNull();
        error.Should().Contain("24 hours");
    }

    [Fact]
    public void Should_ReturnNull_When_ValidationSucceeds()
    {
        // Arrange
        _viewModel.TaskDescription = "Valid task";
        _viewModel.DurationMinutes = 25;
        _viewModel.UseJiraIssue = false;

        // Act
        var error = _viewModel.ValidateInput();

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public async Task Should_LoadIssuesWhenCheckboxChecked_When_JiraIsEnabled()
    {
        // This test is no longer relevant - there's no checkbox to check
        // Issues are loaded automatically in constructor when logged in
        // Arrange
        var issues = new List<JiraIssue>
        {
            new() { Key = "PROJ-1", Summary = "Issue 1", Status = "To Do" }
        };
        _mockJiraService.Setup(x => x.IsEnabled).Returns(true);
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(true);
        _mockJiraService.Setup(x => x.GetAssignedIssuesAsync())
            .ReturnsAsync(issues);

        // Create new ViewModel - should load issues automatically
        var viewModel = new SessionConfigDialogViewModel(_mockJiraService.Object, _mockAuthService.Object);

        // Wait for async loading to complete
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert - issues should be loaded automatically
        viewModel.JiraIssues.Should().HaveCount(1);
        viewModel.JiraIssues[0].Key.Should().Be("PROJ-1");
    }
}
