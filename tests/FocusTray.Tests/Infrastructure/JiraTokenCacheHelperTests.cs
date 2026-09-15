using AwesomeAssertions;
using FocusTray.Infrastructure.Jira;
using Xunit;

namespace FocusTray.Tests.Infrastructure;

public class JiraTokenCacheHelperTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly JiraTokenCacheHelper _helper;

    public JiraTokenCacheHelperTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FocusTrayTests_" + Guid.NewGuid());
        _helper = new JiraTokenCacheHelper(logger: null, cacheDirectory: _tempDirectory);
    }

    [Fact]
    public void Should_ReturnNull_When_NoCacheFileExists()
    {
        var result = _helper.Load();

        result.Should().BeNull();
    }

    [Fact]
    public void Should_RoundTripData_When_SavingAndLoading()
    {
        var data = new JiraTokenCacheData
        {
            AccessToken = "access-123",
            RefreshToken = "refresh-456",
            ExpiresAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CloudId = "cloud-abc",
            SiteUrl = "https://example.atlassian.net",
            SiteName = "Example",
            Email = "user@example.com",
            DisplayName = "Test User"
        };

        _helper.Save(data);
        var loaded = _helper.Load();

        loaded.Should().NotBeNull();
        loaded!.AccessToken.Should().Be("access-123");
        loaded.RefreshToken.Should().Be("refresh-456");
        loaded.ExpiresAtUtc.Should().Be(data.ExpiresAtUtc);
        loaded.CloudId.Should().Be("cloud-abc");
        loaded.SiteUrl.Should().Be("https://example.atlassian.net");
        loaded.Email.Should().Be("user@example.com");
        loaded.DisplayName.Should().Be("Test User");
    }

    [Fact]
    public void Should_ReturnNull_When_LoadingAfterClear()
    {
        _helper.Save(new JiraTokenCacheData { AccessToken = "x" });
        _helper.Clear();

        var result = _helper.Load();

        result.Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
