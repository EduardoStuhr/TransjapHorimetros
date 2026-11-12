using System.Globalization;
using TransjapHorimetros.Application.Ocr;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.UnitTests;

public sealed class OcrQualityTests
{
    [Theory]
    [InlineData("1250", "1250")]
    [InlineData("1250.5", "1250.5")]
    [InlineData("1.250,5", "1250.5")]
    [InlineData("4.850,7", "4850.7")]
    [InlineData("4850,7 h", "4850.7")]
    [InlineData("0004850.7", "4850.7")]
    [InlineData("HRS 4850,70", "4850.70")]
    [InlineData("1,250.5 HOURS", "1250.5")]
    [InlineData("12.345.678 h", "12345678")]
    [InlineData("9999999999.99 h", "9999999999.99")]
    public void NormalizesOnlyUnambiguousDomainValues(string text, string expected)
    {
        var extraction = new MeterReadingExtractor().Extract(new(text, "test", []), MeterUnit.Hours);
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), extraction.Value);
        Assert.Null(extraction.Confidence);
        Assert.Equal("CONFIDENCE_MISSING", extraction.Reason);
    }

    [Theory]
    [InlineData("1.250 h", "AMBIGUOUS_FORMAT")]
    [InlineData("4 850,7 h", "AMBIGUOUS_FORMAT")]
    [InlineData("1,234 h", "AMBIGUOUS_FORMAT")]
    [InlineData("HOURS 123\nHOURS 456", "AMBIGUOUS_VALUES")]
    [InlineData("123 456", "AMBIGUOUS_VALUES")]
    [InlineData("SERIAL 1250", "NO_VALUE")]
    [InlineData("HOURS -123", "NO_VALUE")]
    [InlineData("HOURS 123.4567", "NO_VALUE")]
    [InlineData("HOURS 10000000000", "NO_VALUE")]
    [InlineData("FUEL 100%", "NO_VALUE")]
    [InlineData("RPM 2500", "NO_VALUE")]
    [InlineData("HOURS 123ABC", "NO_VALUE")]
    [InlineData("TEMP 123", "NO_VALUE")]
    [InlineData("KM 1250", "UNIT_MISMATCH")]
    [InlineData("SN 1234\nsem display", "NO_VALUE")]
    public void ExplainsAmbiguitiesAndRejectedContexts(string text, string reason)
    {
        var result = new MeterReadingExtractor().Extract(new(text, "test", []), MeterUnit.Hours);
        Assert.Null(result.Value);
        Assert.Equal(reason, result.Reason);
    }

    [Fact]
    public void AmbiguousFormattingPreservesPlausibleCandidates()
    {
        var result = new MeterReadingExtractor().Extract(new("HOURS 1.250", null, []), MeterUnit.Hours);
        Assert.Null(result.Value);
        Assert.Contains(result.Candidates!, c => c.Value == 1250m);
        Assert.Contains(result.Candidates!, c => c.Value == 1.25m);
    }

    [Fact]
    public void ConfidenceComesFromNumericTokenAndCandidateCountIsBounded()
    {
        var extractor = new MeterReadingExtractor();
        var result = extractor.Extract(new("HOURS 4.850,7", null,
            [new("HOURS", 1m), new("4.850,7", 0.82m)]), MeterUnit.Hours);
        Assert.Equal(0.82m, result.Confidence);
        var many = extractor.Extract(new(string.Join('\n', Enumerable.Range(1, 40).Select(i => $"HOURS {i}")), null, []), MeterUnit.Hours);
        Assert.Null(many.Value);
        Assert.Equal("TOO_MANY_CANDIDATES", many.Reason);
        Assert.True(many.Candidates!.Count <= 20);
    }

    [Fact]
    public async Task CapacityGateHonorsCancellationWhileAllSlotsAreOccupied()
    {
        using var gate = new OcrProcessingGate(1);
        await gate.EnterAsync(default);
        using var deadline = new CancellationTokenSource(30);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.EnterAsync(deadline.Token));
        gate.Exit();
        await gate.EnterAsync(default);
        gate.Exit();
    }
}
