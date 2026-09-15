using AwesomeAssertions;
using FocusTray.Infrastructure.Jira;
using Microsoft.Kiota.Abstractions;
using Xunit;

namespace FocusTray.Tests.Infrastructure;

public class JiraBearerAuthProviderTests
{
    [Fact]
    public async Task Should_AddBearerHeader_When_TokenIsAvailable()
    {
        var provider = new JiraBearerAuthProvider(() => Task.FromResult<string?>("fake-token"));
        var request = new RequestInformation();

        await provider.AuthenticateRequestAsync(request);

        request.Headers.TryGetValue("Authorization", out var values).Should().BeTrue();
        values.Should().Contain("Bearer fake-token");
    }

    [Fact]
    public async Task Should_Throw_When_NoTokenIsAvailable()
    {
        var provider = new JiraBearerAuthProvider(() => Task.FromResult<string?>(null));
        var request = new RequestInformation();

        var act = async () => await provider.AuthenticateRequestAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
