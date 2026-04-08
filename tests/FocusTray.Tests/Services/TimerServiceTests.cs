using AwesomeAssertions;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using Xunit;

namespace FocusTray.Tests.Services;

public class TimerServiceTests : IDisposable
{
    private readonly TimerService _timerService;

    public TimerServiceTests()
    {
        _timerService = new TimerService();
    }

    [Fact]
    public void Should_InitializeWithNoActiveSession_When_Constructed()
    {
        // Assert
        _timerService.CurrentSession.Should().BeNull();
        _timerService.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Should_StartSession_When_ValidParametersProvided()
    {
        // Arrange
        var taskDescription = "Complete unit tests";
        var duration = TimeSpan.FromMinutes(25);

        // Act
        var result = _timerService.StartSession(taskDescription, duration);

        // Assert
        result.Should().BeTrue();
        _timerService.IsRunning.Should().BeTrue();
        _timerService.CurrentSession.Should().NotBeNull();
        _timerService.CurrentSession!.TaskDescription.Should().Be(taskDescription);
        _timerService.CurrentSession.Duration.Should().Be(duration);
        _timerService.CurrentSession.State.Should().Be(TimerState.Running);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Should_ThrowArgumentException_When_TaskDescriptionIsEmpty(string? taskDescription)
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(25);

        // Act
        var act = () => _timerService.StartSession(taskDescription!, duration);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("taskDescription");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Should_ThrowArgumentException_When_DurationIsZeroOrNegative(int minutes)
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(minutes);

        // Act
        var act = () => _timerService.StartSession("Test Task", duration);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("duration");
    }

    [Fact]
    public void Should_ThrowArgumentException_When_DurationExceeds24Hours()
    {
        // Arrange
        var duration = TimeSpan.FromHours(25);

        // Act
        var act = () => _timerService.StartSession("Test Task", duration);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("duration");
    }

    [Fact]
    public void Should_ThrowInvalidOperationException_When_SessionAlreadyRunning()
    {
        // Arrange
        _timerService.StartSession("First Task", TimeSpan.FromMinutes(25));

        // Act
        var act = () => _timerService.StartSession("Second Task", TimeSpan.FromMinutes(30));

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_PauseSessionSuccessfully_When_SessionIsRunning()
    {
        // Arrange
        _timerService.StartSession("Test Task", TimeSpan.FromMinutes(25));

        // Act
        var result = _timerService.PauseSession();

        // Assert
        result.Should().BeTrue();
        _timerService.IsRunning.Should().BeFalse();
        _timerService.CurrentSession!.State.Should().Be(TimerState.Paused);
    }

    [Fact]
    public void Should_ReturnFalse_When_PausingWithNoActiveSession()
    {
        // Act
        var result = _timerService.PauseSession();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Should_ResumeSessionSuccessfully_When_SessionIsPaused()
    {
        // Arrange
        _timerService.StartSession("Test Task", TimeSpan.FromMinutes(25));
        _timerService.PauseSession();

        // Act
        var result = _timerService.ResumeSession();

        // Assert
        result.Should().BeTrue();
        _timerService.IsRunning.Should().BeTrue();
        _timerService.CurrentSession!.State.Should().Be(TimerState.Running);
    }

    [Fact]
    public void Should_ReturnFalse_When_ResumingNonPausedSession()
    {
        // Act
        var result = _timerService.ResumeSession();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Should_StopSessionSuccessfully_When_SessionIsRunning()
    {
        // Arrange
        _timerService.StartSession("Test Task", TimeSpan.FromMinutes(25));

        // Act
        var result = _timerService.StopSession();

        // Assert
        result.Should().BeTrue();
        _timerService.IsRunning.Should().BeFalse();
        _timerService.CurrentSession.Should().BeNull();
    }

    [Fact]
    public void Should_ReturnFalse_When_StoppingWithNoActiveSession()
    {
        // Act
        var result = _timerService.StopSession();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Should_ExtendSessionDuration_When_ValidTimeProvided()
    {
        // Arrange
        var initialDuration = TimeSpan.FromMinutes(25);
        var extension = TimeSpan.FromMinutes(15);
        _timerService.StartSession("Test Task", initialDuration);

        // Act
        var result = _timerService.ExtendSession(extension);

        // Assert
        result.Should().BeTrue();
        _timerService.CurrentSession!.Duration.Should().Be(initialDuration + extension);
    }

    [Fact]
    public void Should_ReturnFalse_When_ExtendingNonRunningSession()
    {
        // Act
        var result = _timerService.ExtendSession(TimeSpan.FromMinutes(15));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Should_ThrowArgumentException_When_ExtendingWithNegativeTime()
    {
        // Arrange
        _timerService.StartSession("Test Task", TimeSpan.FromMinutes(25));

        // Act
        var act = () => _timerService.ExtendSession(TimeSpan.FromMinutes(-5));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("additionalTime");
    }

    [Fact]
    public void Should_ThrowArgumentException_When_ExtensionExceedsMaxDuration()
    {
        // Arrange
        _timerService.StartSession("Test Task", TimeSpan.FromHours(20));

        // Act
        var act = () => _timerService.ExtendSession(TimeSpan.FromHours(5));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("additionalTime");
    }

    [Fact]
    public void Should_RaiseStateChangedEvent_When_SessionStarts()
    {
        // Arrange
        TimerState? raisedState = null;
        _timerService.StateChanged += (sender, state) => raisedState = state;

        // Act
        _timerService.StartSession("Test Task", TimeSpan.FromMinutes(25));

        // Assert
        raisedState.Should().Be(TimerState.Running);
    }

    [Fact]
    public void Should_RaiseStateChangedEvent_When_SessionPaused()
    {
        // Arrange
        _timerService.StartSession("Test Task", TimeSpan.FromMinutes(25));
        TimerState? raisedState = null;
        _timerService.StateChanged += (sender, state) => raisedState = state;

        // Act
        _timerService.PauseSession();

        // Assert
        raisedState.Should().Be(TimerState.Paused);
    }

    [Fact]
    public void Should_RaiseTickEventPeriodically_When_SessionIsRunning()
    {
        // Arrange
        var tickCount = 0;
        _timerService.Tick += (sender, timeRemaining) => tickCount++;
        _timerService.StartSession("Test Task", TimeSpan.FromSeconds(3));

        // Act
        Thread.Sleep(2500); // Wait for 2-3 ticks

        // Assert
        tickCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Should_RaiseSessionCompletedEvent_When_TimerExpires()
    {
        // Arrange
        FocusSession? completedSession = null;
        _timerService.SessionCompleted += (sender, session) => completedSession = session;
        _timerService.StartSession("Test Task", TimeSpan.FromSeconds(2));

        // Act
        await Task.Delay(2500, TestContext.Current.CancellationToken); // Wait for completion

        // Assert
        completedSession.Should().NotBeNull();
        completedSession!.State.Should().Be(TimerState.Completed);
    }

    public void Dispose()
    {
        _timerService.Dispose();
    }
}
