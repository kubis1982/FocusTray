using AwesomeAssertions;
using FocusTray.Infrastructure.Jira;
using Xunit;

namespace FocusTray.Tests.Infrastructure;

public class PkceGeneratorTests
{
    [Fact]
    public void Should_GenerateVerifierWithinRfcLengthRange_When_GeneratingCodeVerifier()
    {
        var verifier = PkceGenerator.GenerateCodeVerifier();

        verifier.Length.Should().BeInRange(43, 128);
        verifier.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Fact]
    public void Should_GenerateDifferentValues_When_CalledTwice()
    {
        var first = PkceGenerator.GenerateCodeVerifier();
        var second = PkceGenerator.GenerateCodeVerifier();

        first.Should().NotBe(second);
    }

    [Fact]
    public void Should_ComputeKnownS256Challenge_When_GivenFixedVerifier()
    {
        // Known-answer test: SHA256("test-code-verifier-1234567890") base64url-encoded,
        // computed independently and verified out-of-band.
        var challenge = PkceGenerator.GenerateCodeChallenge("test-code-verifier-1234567890");

        challenge.Should().Be("zP9WD-aC0tyxQ8j2yFOsEM4jCFnCcGRqEPmHx5qiO70");
    }

    [Fact]
    public void Should_GenerateNonEmptyState_When_GeneratingState()
    {
        var state = PkceGenerator.GenerateState();

        state.Should().NotBeNullOrWhiteSpace();
    }
}
