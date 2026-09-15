using AwesomeAssertions;
using FocusTray.Infrastructure.Jira;
using Xunit;

namespace FocusTray.Tests.Infrastructure;

public class JiraCallbackListenerTests
{
    [Fact]
    public void Should_ParseCodeAndState_When_QueryContainsSuccessParameters()
    {
        var result = JiraCallbackListener.ParseCallbackQuery("?code=abc123&state=xyz789");

        result.Code.Should().Be("abc123");
        result.State.Should().Be("xyz789");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Should_ParseErrorFields_When_QueryContainsAccessDenied()
    {
        var result = JiraCallbackListener.ParseCallbackQuery(
            "?error=access_denied&error_description=User%20denied%20access&state=xyz789");

        result.Error.Should().Be("access_denied");
        result.ErrorDescription.Should().Be("User denied access");
        result.State.Should().Be("xyz789");
        result.Code.Should().BeNull();
    }

    [Fact]
    public void Should_ReturnEmptyResult_When_QueryIsEmpty()
    {
        var result = JiraCallbackListener.ParseCallbackQuery("");

        result.Code.Should().BeNull();
        result.State.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task Should_CaptureCodeAndState_When_RealHttpRequestHitsLoopback()
    {
        const string redirectUri = "http://localhost:18089/callback";
        var listener = new JiraCallbackListener();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var callbackTask = listener.WaitForCallbackAsync(redirectUri, cts.Token);

        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync($"{redirectUri}?code=abc123&state=xyz789");
        response.EnsureSuccessStatusCode();

        var result = await callbackTask;

        result.Code.Should().Be("abc123");
        result.State.Should().Be("xyz789");
    }
}
