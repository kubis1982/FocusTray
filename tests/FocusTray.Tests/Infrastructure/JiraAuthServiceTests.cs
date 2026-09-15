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

public class JiraAuthServiceTests : IDisposable
{
    private readonly Mock<ICredentialService> _mockCredentialService;
    private readonly Mock<IJiraSitePickerPrompt> _mockSitePickerPrompt;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<JiraAuthService>> _mockLogger;
    private readonly string _tempCacheDirectory;

    public JiraAuthServiceTests()
    {
        _mockCredentialService = new Mock<ICredentialService>();
        _mockCredentialService.Setup(x => x.HasStoredCredentials("FocusTray_Jira")).Returns(false);

        _mockSitePickerPrompt = new Mock<IJiraSitePickerPrompt>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _mockLogger = new Mock<ILogger<JiraAuthService>>();
        _tempCacheDirectory = Path.Combine(Path.GetTempPath(), "FocusTrayTests_" + Guid.NewGuid());
    }

    private JiraAuthService CreateService() =>
        new(_mockCredentialService.Object, _mockSitePickerPrompt.Object, _httpClient, _mockLogger.Object, _tempCacheDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_tempCacheDirectory))
        {
            Directory.Delete(_tempCacheDirectory, recursive: true);
        }
    }

    [Fact]
    public void Should_NotDeleteCredentials_When_NoLegacyCredentialsStored()
    {
        _mockCredentialService.Setup(x => x.HasStoredCredentials("FocusTray_Jira")).Returns(false);

        _ = CreateService();

        _mockCredentialService.Verify(x => x.DeleteCredentials(It.IsAny<string>()), Times.Never);
    }

    // NOTE: the placeholder-ClientId guard (RemoveLegacyBasicAuthCredentials skipping deletion,
    // LoginAsync failing fast) is exercised as a pure predicate in
    // JiraOAuthConfigurationTests.Should_DetectPlaceholder_When_GivenThePlaceholderClientIdValue,
    // since JiraOAuthConfiguration.ClientId is a compile-time const and this environment now
    // has a real, registered Atlassian Client ID configured (so the guard's "placeholder"
    // branch is not reachable live in this build to assert against here).

    [Fact]
    public async Task Should_ReturnFalse_When_TryAutoLoginWithNoStoredCache()
    {
        var service = CreateService();

        var result = await service.TryAutoLoginAsync();

        result.Should().BeFalse();
        service.IsLoggedIn.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReturnNull_When_GetAccessTokenCalledBeforeAnyLogin()
    {
        var service = CreateService();

        var token = await service.GetAccessTokenAsync();

        token.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnTrue_When_LogoutCalledWithoutPriorLogin()
    {
        var service = CreateService();

        var result = await service.LogoutAsync();

        result.Should().BeTrue();
        service.IsLoggedIn.Should().BeFalse();
    }

    private void SetupAccessibleResourcesResponse(string json)
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString() == JiraOAuthConfiguration.AccessibleResourcesEndpoint),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task Should_ReturnSoleSite_When_OnlyOneAccessibleResource()
    {
        SetupAccessibleResourcesResponse(
            "[{\"id\":\"cloud-1\",\"url\":\"https://one.atlassian.net\",\"name\":\"One\"}]");
        var service = CreateService();

        var site = await service.ResolveSiteAsync("fake-token");

        site.Should().NotBeNull();
        site!.Id.Should().Be("cloud-1");
        _mockSitePickerPrompt.Verify(
            x => x.PickSiteAsync(It.IsAny<IReadOnlyList<JiraAccessibleResource>>()), Times.Never);
    }

    [Fact]
    public async Task Should_InvokeSitePicker_When_MultipleAccessibleResources()
    {
        SetupAccessibleResourcesResponse(
            "[{\"id\":\"cloud-1\",\"url\":\"https://one.atlassian.net\",\"name\":\"One\"}," +
            "{\"id\":\"cloud-2\",\"url\":\"https://two.atlassian.net\",\"name\":\"Two\"}]");
        var expectedChoice = new JiraAccessibleResource { Id = "cloud-2", Url = "https://two.atlassian.net", Name = "Two" };
        _mockSitePickerPrompt
            .Setup(x => x.PickSiteAsync(It.Is<IReadOnlyList<JiraAccessibleResource>>(s => s.Count == 2)))
            .ReturnsAsync(expectedChoice);
        var service = CreateService();

        var site = await service.ResolveSiteAsync("fake-token");

        site.Should().BeSameAs(expectedChoice);
        _mockSitePickerPrompt.Verify(
            x => x.PickSiteAsync(It.IsAny<IReadOnlyList<JiraAccessibleResource>>()), Times.Once);
    }

    [Fact]
    public async Task Should_ReturnNull_When_NoAccessibleResources()
    {
        SetupAccessibleResourcesResponse("[]");
        var service = CreateService();

        var site = await service.ResolveSiteAsync("fake-token");

        site.Should().BeNull();
    }
}
