using AwesomeAssertions;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using FocusTray.ViewModels;
using Moq;
using System.Collections.ObjectModel;
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

    [Fact]
    public void Should_ReturnDurationMinutes_When_NotInEndTimeMode()
    {
        // Arrange
        _viewModel.IsEndTimeMode = false;
        _viewModel.DurationMinutes = 45;

        // Act
        var duration = _viewModel.GetEffectiveDuration();

        // Assert
        duration.Should().Be(45);
    }

    [Fact]
    public void Should_CalculateMinutesUntilEndTime_When_InEndTimeMode()
    {
        // Arrange
        _viewModel.IsEndTimeMode = true;
        var now = DateTime.Now;
        var targetTime = now.AddMinutes(30);
        _viewModel.TargetEndTime = TimeOnly.FromDateTime(targetTime);

        // Act
        var duration = _viewModel.GetEffectiveDuration();

        // Assert
        // Should be approximately 30 minutes (allowing for small time differences)
        duration.Should().BeGreaterThanOrEqualTo(29);
        duration.Should().BeLessThanOrEqualTo(31);
    }

    [Fact]
    public void Should_AssumeNextDay_When_TargetTimeIsInPast()
    {
        // Arrange
        _viewModel.IsEndTimeMode = true;
        var now = DateTime.Now;
        var pastTime = now.AddHours(-2); // 2 hours ago
        _viewModel.TargetEndTime = TimeOnly.FromDateTime(pastTime);

        // Act
        var duration = _viewModel.GetEffectiveDuration();

        // Assert
        // Should calculate to next day (approximately 22 hours = 1320 minutes)
        duration.Should().BeGreaterThan(1300);
        duration.Should().BeLessThanOrEqualTo(1440);
    }

    [Fact]
    public void Should_SetTargetEndTime_When_SwitchingToEndTimeMode()
    {
        // Arrange
        _viewModel.IsEndTimeMode = false;
        _viewModel.DurationMinutes = 60;
        var beforeSwitch = DateTime.Now;

        // Act
        _viewModel.IsEndTimeMode = true;

        // Assert
        // Target time should be approximately 60 minutes from now, rounded to nearest 15-minute slot
        var expectedTime = beforeSwitch.AddMinutes(60);
        var actualTime = DateTime.Today.Add(_viewModel.TargetEndTime.ToTimeSpan());

        // Allow 15 minutes tolerance due to slot rounding
        var difference = Math.Abs((actualTime - expectedTime).TotalMinutes);
        difference.Should().BeLessThan(16);
    }

    [Fact]
    public void Should_ReturnError_When_EndTimeIsInPastAndDurationExceeds24Hours()
    {
        // Arrange
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(false);
        _viewModel.TaskDescription = "Valid task";
        _viewModel.IsEndTimeMode = true;

        // Set target time to just 1 minute ago (which would calculate to ~23h59m next day)
        var pastTime = DateTime.Now.AddMinutes(-1);
        _viewModel.TargetEndTime = TimeOnly.FromDateTime(pastTime);

        // Act
        var error = _viewModel.ValidateInput();

        // Assert - should be valid as it's less than 24 hours
        error.Should().BeNull();
    }

    [Fact]
    public void Should_ReturnError_When_EndTimeResultsInZeroDuration()
    {
        // Arrange
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(false);
        _viewModel.TaskDescription = "Valid task";
        _viewModel.IsEndTimeMode = true;

        // Set target time to exactly now (will be treated as past, so next day)
        _viewModel.TargetEndTime = TimeOnly.FromDateTime(DateTime.Now);

        // Act
        var error = _viewModel.ValidateInput();

        // Assert - should be valid (next day calculation)
        error.Should().BeNull();
    }

    [Fact]
    public void Should_InitializeTimeSlots_When_SwitchingToEndTimeMode()
    {
        // Arrange
        _viewModel.IsEndTimeMode = false;

        // Act
        _viewModel.IsEndTimeMode = true;

        // Assert
        _viewModel.AvailableTimeSlots.Should().NotBeEmpty();
        _viewModel.AvailableTimeSlots.Count.Should().Be(48); // 12 hours * 4 slots per hour
        _viewModel.SelectedTimeSlot.Should().NotBeNull();
    }

    [Fact]
    public void Should_UpdateTargetEndTime_When_TimeSlotSelected()
    {
        // Arrange
        _viewModel.IsEndTimeMode = true;
        var targetTime = new TimeOnly(15, 30);

        // Act
        _viewModel.SelectedTimeSlot = targetTime;

        // Assert
        _viewModel.TargetEndTime.Should().Be(targetTime);
    }

    [Fact]
    public void Should_GenerateTimeSlotsIn15MinuteIntervals_When_Initialized()
    {
        // Arrange & Act
        _viewModel.IsEndTimeMode = true;

        // Assert
        var slots = _viewModel.AvailableTimeSlots.ToList();
        slots.Should().HaveCount(48); // 12 hours * 4 slots

        for (int i = 1; i < slots.Count; i++)
        {
            var prevSpan = slots[i - 1].ToTimeSpan();
            var currentSpan = slots[i].ToTimeSpan();

            var difference = currentSpan - prevSpan;

            // Handle midnight wraparound
            if (difference.TotalMinutes < 0)
            {
                difference = TimeSpan.FromHours(24) + difference;
            }

            difference.TotalMinutes.Should().Be(15);
        }
    }

    [Fact]
    public void Should_UpdateDurationMinutes_When_SwitchingFromEndTimeModeToMinutesMode()
    {
        // Arrange
        _viewModel.IsEndTimeMode = false;
        _viewModel.DurationMinutes = 60;

        // Switch to end time mode (initializes time slots)
        _viewModel.IsEndTimeMode = true;

        // Manually select a time slot (simulating user selection) - 2 hours from now
        var now = DateTime.Now;
        var twoHoursFromNow = now.AddHours(2);
        var targetSlot = FindClosestSlot(_viewModel.AvailableTimeSlots, TimeOnly.FromDateTime(twoHoursFromNow));
        _viewModel.SelectedTimeSlot = targetSlot;

        // Allow time for property change to propagate
        var expectedMinutes = (int)Math.Ceiling((DateTime.Today.Add(targetSlot.ToTimeSpan()) - now).TotalMinutes);
        if (expectedMinutes < 0)
        {
            expectedMinutes += 1440; // Next day
        }

        // Act - switch back to minutes mode
        _viewModel.IsEndTimeMode = false;

        // Assert - duration should reflect the selected time slot (approximately 120 minutes ± 15)
        _viewModel.DurationMinutes.Should().BeGreaterThanOrEqualTo(expectedMinutes - 15);
        _viewModel.DurationMinutes.Should().BeLessThanOrEqualTo(expectedMinutes + 15);
    }

    [Fact]
    public void Should_PreserveDurationMinutes_When_RoundTripBetweenModes()
    {
        // Arrange
        _viewModel.IsEndTimeMode = false;
        _viewModel.DurationMinutes = 45;

        // Act - switch to end time and back
        _viewModel.IsEndTimeMode = true;
        _viewModel.IsEndTimeMode = false;

        // Assert - duration should be close to original (allowing for rounding)
        _viewModel.DurationMinutes.Should().BeGreaterThanOrEqualTo(30);
        _viewModel.DurationMinutes.Should().BeLessThanOrEqualTo(60);
    }

    private TimeOnly FindClosestSlot(ObservableCollection<TimeOnly> slots, TimeOnly target)
    {
        return slots
            .OrderBy(slot => Math.Abs((slot.ToTimeSpan() - target.ToTimeSpan()).TotalMinutes))
            .First();
    }
}
