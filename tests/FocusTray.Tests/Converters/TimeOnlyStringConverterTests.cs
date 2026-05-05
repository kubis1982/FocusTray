using AwesomeAssertions;
using FocusTray.Converters;
using System.Globalization;
using Xunit;

namespace FocusTray.Tests.Converters;

public class TimeOnlyStringConverterTests
{
    private readonly TimeOnlyStringConverter _converter = new();

    [Fact]
    public void Should_ConvertTimeOnlyToString_When_ValidTimeProvided()
    {
        // Arrange
        var time = new TimeOnly(14, 30);

        // Act
        var result = _converter.Convert(time, typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("14:30");
    }

    [Fact]
    public void Should_ConvertBackStringToTimeOnly_When_ValidFormatProvided()
    {
        // Arrange
        var timeString = "15:45";

        // Act
        var result = _converter.ConvertBack(timeString, typeof(TimeOnly), null!, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(new TimeOnly(15, 45));
    }

    [Fact]
    public void Should_ConvertBackWithSingleDigitHour_When_ShortFormatProvided()
    {
        // Arrange
        var timeString = "9:30";

        // Act
        var result = _converter.ConvertBack(timeString, typeof(TimeOnly), null!, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(new TimeOnly(9, 30));
    }

    [Fact]
    public void Should_ReturnCurrentTime_When_InvalidStringProvided()
    {
        // Arrange
        var timeString = "invalid";
        var beforeConversion = DateTime.Now;

        // Act
        var result = (TimeOnly)_converter.ConvertBack(timeString, typeof(TimeOnly), null!, CultureInfo.InvariantCulture);

        // Assert
        // Should be approximately current time (within a few seconds)
        var resultDateTime = DateTime.Today.Add(result.ToTimeSpan());
        var difference = Math.Abs((resultDateTime - beforeConversion).TotalMinutes);
        difference.Should().BeLessThan(1);
    }

    [Fact]
    public void Should_ReturnEmptyString_When_NullValueProvided()
    {
        // Act
        var result = _converter.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void Should_HandleMidnight_When_Converting()
    {
        // Arrange
        var time = new TimeOnly(0, 0);

        // Act
        var result = _converter.Convert(time, typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("00:00");
    }

    [Fact]
    public void Should_HandleEndOfDay_When_Converting()
    {
        // Arrange
        var time = new TimeOnly(23, 59);

        // Act
        var result = _converter.Convert(time, typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("23:59");
    }
}
