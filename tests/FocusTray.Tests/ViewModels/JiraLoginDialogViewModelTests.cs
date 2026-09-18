using AwesomeAssertions;
using FocusTray.Core.Services;
using FocusTray.Infrastructure.Jira;
using FocusTray.Services;
using FocusTray.ViewModels;
using Moq;
using Xunit;

namespace FocusTray.Tests.ViewModels;

public class JiraLoginDialogViewModelTests
{
    private readonly Mock<IJiraAuthService> _mockAuthService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly JiraConfiguration _configuration;
    private readonly JiraLoginDialogViewModel _viewModel;

    public JiraLoginDialogViewModelTests()
    {
        _mockAuthService = new Mock<IJiraAuthService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _configuration = new JiraConfiguration();

        _viewModel = new JiraLoginDialogViewModel(
            _mockAuthService.Object,
            _mockSettingsService.Object,
            _configuration);
    }

    [Fact]
    public async Task Should_SaveJiraConfiguration_When_LoginSucceeds()
    {
        // Arrange
        _viewModel.Company = "acme";
        _viewModel.Email = "user@example.com";
        _viewModel.ApiToken = "token123";

        _mockAuthService
            .Setup(x => x.LoginAsync("acme", "user@example.com", "token123"))
            .Callback(() => _configuration.Company = "acme")
            .ReturnsAsync(true);
        _mockAuthService.Setup(x => x.CurrentUsername).Returns("Test User");

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _viewModel.IsSuccess.Should().BeTrue();
        _mockSettingsService.Verify(x => x.SaveJiraConfiguration(_configuration), Times.Once);
    }

    [Fact]
    public async Task Should_NotSaveJiraConfiguration_When_LoginFails()
    {
        // Arrange
        _viewModel.Company = "acme";
        _viewModel.Email = "user@example.com";
        _viewModel.ApiToken = "wrong-token";

        _mockAuthService
            .Setup(x => x.LoginAsync("acme", "user@example.com", "wrong-token"))
            .ReturnsAsync(false);

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _viewModel.IsSuccess.Should().BeFalse();
        _mockSettingsService.Verify(x => x.SaveJiraConfiguration(It.IsAny<JiraConfiguration>()), Times.Never);
    }

    [Fact]
    public async Task Should_NotAttemptLogin_When_CompanyIsEmpty()
    {
        // Arrange
        _viewModel.Company = "";
        _viewModel.Email = "user@example.com";
        _viewModel.ApiToken = "token123";

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _mockAuthService.Verify(
            x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _mockSettingsService.Verify(x => x.SaveJiraConfiguration(It.IsAny<JiraConfiguration>()), Times.Never);
    }
}
