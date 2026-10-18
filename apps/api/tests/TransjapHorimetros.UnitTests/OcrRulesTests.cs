using TransjapHorimetros.Application.Ocr;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.UnitTests;

public sealed class OcrRulesTests
{
    [Theory]
    [InlineData("1234.5", null, "1234.5")]
    [InlineData("1234,5", null, "1234.5")]
    [InlineData("HORÍMETRO 1234.5", MeterUnit.Hours, "1234.5")]
    [InlineData("HORIMETRO 1234,5", MeterUnit.Hours, "1234.5")]
    [InlineData("HOURS 001234.5", MeterUnit.Hours, "1234.5")]
    [InlineData("ODO 123456", MeterUnit.Km, "123456")]
    [InlineData("123456 KM", MeterUnit.Km, "123456")]
    [InlineData("2026-10-18\nHORIMETRO 1234.5", null, "1234.5")]
    [InlineData("18/10/2026 12:30\nHOURS 1234.5", MeterUnit.Hours, "1234.5")]
    [InlineData("SERIAL 999999\nHOURS 1234.5", null, "1234.5")]
    [InlineData("HOURS 1234.5\nODO 123456", MeterUnit.Hours, "1234.5")]
    [InlineData("HOURS 1234.5\nODO 123456", MeterUnit.Km, "123456")]
    [InlineData("HOURS 1234.5\nODO 123456", null, null)]
    [InlineData("HOURS 1234.5\nHOURS 2234.5", MeterUnit.Hours, null)]
    [InlineData("123 456", null, null)]
    [InlineData("Sem leitura", null, null)]
    [InlineData("SN 123456", null, null)]
    [InlineData("12:30", null, null)]
    [InlineData("18/10/2026", null, null)]
    [InlineData("HOURS 123456789012345678", MeterUnit.Hours, null)]
    [InlineData("HOURS -123.4", MeterUnit.Hours, null)]
    [InlineData("ABC1234", null, null)]
    [InlineData("1,234.5", null, null)]
    [InlineData("KM 123456", MeterUnit.Hours, null)]
    public void Extract_Conservatively(string text, MeterUnit? unit, string? expected)
    {
        var result = new MeterReadingExtractor().Extract(new(text, null, []), unit);
        Assert.Equal(expected is null ? (decimal?)null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), result.Value);
        Assert.Null(result.Confidence);
    }

    [Theory]
    [InlineData("0.91", "0.91")]
    [InlineData(null, null)]
    [InlineData("1.2", null)]
    public void Confidence_UsesOnlyNumericWord(string? source, string? expected)
    {
        decimal? Parse(string? v) => v is null ? null : decimal.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
        var extraction = new MeterReadingExtractor().Extract(new("HOURS 1234.5", null,
            [new("HOURS", 0.99m), new("1234.5", Parse(source))]), MeterUnit.Hours);
        Assert.Equal(Parse(expected), extraction.Confidence);
    }

    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(100, 100.1, true)]
    [InlineData(100, 101, false)]
    [InlineData(1, 1.1, false)]
    [InlineData(0, 0, true)]
    [InlineData(0, 0.1, true)]
    [InlineData(0, 1, false)]
    public void Comparison_AbsoluteOrPercentage(decimal value, decimal detected, bool matches)
    {
        var result = OcrComparison.Evaluate(value, new(detected, null), new());
        Assert.Equal(matches, result.Matches);
        Assert.Equal(Math.Abs(value - detected), result.AbsoluteDifference);
        if (value == 0) Assert.Null(result.PercentageDifference);
        Assert.False(result.LowConfidence);
    }

    [Theory]
    [InlineData("0.5", true)]
    [InlineData("0.75", false)]
    [InlineData(null, false)]
    public void Comparison_LowOrMissingConfidence(string? confidence, bool low)
    {
        var c = confidence is null ? (decimal?)null : decimal.Parse(confidence, System.Globalization.CultureInfo.InvariantCulture);
        var result = OcrComparison.Evaluate(100, new(null, c), new());
        Assert.Equal(low, result.LowConfidence);
        Assert.Null(result.AbsoluteDifference); Assert.Null(result.Matches);
    }

    [Fact]
    public void CompletedResult_TruncatesTextAndCannotBeOverwritten()
    {
        var result = new PhotoOcrResult(Guid.NewGuid(), "Fake", true, DateTimeOffset.UtcNow, "test", null);
        void Complete() => result.Complete(PhotoOcrStatus.Succeeded, new string('x', 8000), null,
            100, null, 0, 0, true, DateTimeOffset.UtcNow, 10);
        Complete();
        Assert.Equal(4000, result.RawText!.Length);
        Assert.Throws<InvalidOperationException>(Complete);
    }

    [Fact]
    public void TextLimit_DoesNotCreateTruncatedNumericCandidate()
    {
        var text = new string(' ', 3988) + "HOURS 123456789012345";
        var limited = OcrText.Limit(text);
        Assert.True(limited.Length <= 4000);
        Assert.Null(new MeterReadingExtractor().Extract(new(limited, null, []), null).Value);
    }
}
