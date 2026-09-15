using AwesomeAssertions;
using FocusTray.Infrastructure.Jira;
using Xunit;

namespace FocusTray.Tests.Infrastructure;

public class JiraOAuthConfigurationTests
{
    [Fact]
    public void Should_BuildApiBaseUrl_When_CloudIdProvided()
    {
        var result = JiraOAuthConfiguration.BuildApiBaseUrl("abc-123");

        result.Should().Be("https://api.atlassian.com/ex/jira/abc-123/");
    }

    [Fact]
    public void Should_Throw_When_BuildApiBaseUrlCalledWithEmptyCloudId()
    {
        var act = () => JiraOAuthConfiguration.BuildApiBaseUrl("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_IncludeRequiredParameters_When_BuildingAuthorizationUrl()
    {
        var url = JiraOAuthConfiguration.BuildAuthorizationUrl("test-challenge", "test-state");

        url.Should().StartWith("https://auth.atlassian.com/authorize?");
        url.Should().Contain("audience=api.atlassian.com");
        url.Should().Contain($"client_id={JiraOAuthConfiguration.ClientId}");
        url.Should().Contain("code_challenge=test-challenge");
        url.Should().Contain("code_challenge_method=S256");
        url.Should().Contain("state=test-state");
        url.Should().Contain("response_type=code");
        url.Should().Contain(Uri.EscapeDataString(JiraOAuthConfiguration.RedirectUri));
    }
}
