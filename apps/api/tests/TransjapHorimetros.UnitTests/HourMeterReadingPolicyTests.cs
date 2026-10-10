using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Domain.Rules;

namespace TransjapHorimetros.UnitTests;

/// <summary>
/// Testes unitários para HourMeterReadingPolicy.
/// Cobrem: leitura normal, regressiva, duplicata, aumento plausível/impossível,
/// clock skew, virada de dia, limites exatos de tolerância e status resultante.
/// </summary>
public sealed class HourMeterReadingPolicyTests
{
    private readonly HourMeterReadingPolicy _policy = new();
    private static readonly DateTimeOffset BaseTime = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);

    // Opções padrão para a maioria dos testes
    private static readonly HourMeterRulesOptions DefaultOptions = new()
    {
        ElapsedTimeToleranceHours = 0.25m,
        MaxPlausibleDailyHours = 24.0m,
        MaxPlausibleHoursPerElapsedHour = 1.0m,
        DayTransitionToleranceHours = 0.5m,
        ClockSkewToleranceSeconds = 300,
        MissingReadingThresholdDays = 2,
        GeofenceDefaultRadiusMeters = 500,
        OcrConfidenceThreshold = 0.85m,
    };

    // ────────────────────────────────────────────────
    // Casos: valor negativo → REJECTED
    // ────────────────────────────────────────────────

    [Fact]
    public void Assess_WhenValueIsNegative_RejectsReading()
    {
        var ctx = Context(currentValue: -1m);
        var result = _policy.Assess(ctx, DefaultOptions);

        Assert.Equal(ReadingStatus.Rejected, result.Status);
        Assert.NotNull(result.RejectionReason);
        Assert.Empty(result.Anomalies);
    }

    [Fact]
    public void Assess_WhenValueIsZero_Validates()
    {
        var ctx = Context(currentValue: 0m);
        var result = _policy.Assess(ctx, DefaultOptions);

        Assert.Equal(ReadingStatus.Validated, result.Status);
    }

    // ────────────────────────────────────────────────
    // Casos: sem leitura anterior → VALIDATED
    // ────────────────────────────────────────────────

    [Fact]
    public void Assess_WhenNoPreviousReading_Validates()
    {
        var ctx = new ReadingAssessmentContext(
            PreviousValue: null,
            PreviousCapturedAt: null,
            PreviousReadingType: null,
            CurrentValue: 100m,
            CurrentCapturedAt: BaseTime,
            CurrentReadingType: ReadingType.Opening,
            ReceivedAtServer: BaseTime,
            IsPotentialDuplicate: false);

        var result = _policy.Assess(ctx, DefaultOptions);

        Assert.Equal(ReadingStatus.Validated, result.Status);
        Assert.Empty(result.Anomalies);
    }

    // ────────────────────────────────────────────────
    // Casos: leitura regressiva → READING_DECREASE
    // ────────────────────────────────────────────────

    [Fact]
    public void Assess_WhenCurrentValueIsLower_MarksReadingAsSuspect()
    {
        var ctx = Context(previousValue: 100m, currentValue: 90m, elapsedHours: 1);
        var result = _policy.Assess(ctx, DefaultOptions);

        Assert.Equal(ReadingStatus.Suspect, result.Status);
        Assert.Contains(result.Anomalies, a => a.Type == AnomalyType.ReadingDecrease);
    }

    [Fact]
    public void Assess_WhenCurrentValueEqualsLastValue_Validates()
    {
        var ctx = Context(previousValue: 100m, currentValue: 100m, elapsedHours: 1);
        var result = _policy.Assess(ctx, DefaultOptions);

        // Mesmo valor não é regressivo nem impossível
        Assert.Equal(ReadingStatus.Validated, result.Status);
        Assert.DoesNotContain(result.Anomalies, a => a.Type == AnomalyType.ReadingDecrease);
        Assert.DoesNotContain(result.Anomalies, a => a.Type == AnomalyType.ImpossibleHourIncrease);
    }

    // ────────────────────────────────────────────────
    // Casos: aumento plausível → VALIDATED
    // ────────────────────────────────────────────────

    [Fact]
    public void Assess_WhenIncreaseIsPlausible_ValidatesWithoutAnomaly()
    {
        // 9h transcorridas, aumento de 8.5h: dentro do limite (9h + 0.25h = 9.25h)
        var ctx = Context(previousValue: 100m, currentValue: 108.5m, elapsedHours: 9);
        var result = _policy.Assess(ctx, DefaultOptions);

        Assert.Equal(ReadingStatus.Validated, result.Status);
        Assert.DoesNotContain(result.Anomalies, a => a.Type == AnomalyType.ImpossibleHourIncrease);
    }

    // ────────────────────────────────────────────────
    // Casos: aumento impossível → IMPOSSIBLE_HOUR_INCREASE
    // ────────────────────────────────────────────────

    [Fact]
    public void Assess_WhenIncreaseExceedsElapsedTime_MarksReadingAsSuspect()
    {
        // 1h transcorrida, aumento de 2h: excede 1h + 0.25h = 1.25h
        var ctx = Context(previousValue: 100m, currentValue: 102m, elapsedHours: 1);
        var result = _policy.Assess(ctx, DefaultOptions);

        Assert.Equal(ReadingStatus.Suspect, result.Status);
        Assert.Contains(result.Anomalies, a => a.Type == AnomalyType.ImpossibleHourIncrease);
    }

    // ────────────────────────────────────────────────
    // Casos: limites EXATOS de tolerância (ElapsedTimeToleranceHours = 0.25h)
    // ────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0, 1.24, false)] // limite - 0.01 → dentro do limite
    [InlineData(1.0, 1.25, false)] // exatamente no limite → dentro
    [InlineData(1.0, 1.26, true)]  // limite + 0.01 → ultrapassa
    public void Assess_ElapsedToleranceBoundary(double elapsedHours, double delta, bool expectAnomaly)
    {
        // Com elapsedHours decorridas e MaxPlausibleHoursPerElapsedHour=1.0:
        // limite = elapsedHours * 1.0 + 0.25
        var ctx = Context(
            previousValue: 100m,
            currentValue: 100m + (decimal)delta,
            elapsedHours: elapsedHours);
        var result = _policy.Assess(ctx, DefaultOptions);

        if (expectAnomaly)
        {
            Assert.Contains(result.Anomalies, a => a.Type == AnomalyType.ImpossibleHourIncrease);
        }
        else
        {
            Assert.DoesNotContain(result.Anomalies, a => a.Type == AnomalyType.ImpossibleHourIncrease);
        }
    }

    // ────────────────────────────────────────────────
    // Casos: clock skew
    // ────────────────────────────────────────────────

    [Fact]
    public void Assess_WhenClockSkewExceedsThreshold_GeneratesAnomaly()
    {
        // Dispositivo capturou 10 min antes do servidor → 600s > 300s
        var capturedAt = BaseTime;
        var receivedAt = BaseTime.AddSeconds(600);
        var ctx = new ReadingAssessmentContext(
            PreviousValue: null,
            PreviousCapturedAt: null,
            PreviousReadingType: null,
            CurrentValue: 100m,
            CurrentCapturedAt: capturedAt,
            CurrentReadingType: ReadingType.Opening,
            ReceivedAtServer: receivedAt,
            IsPotentialDuplicate: false);

        var result = _policy.Assess(ctx, DefaultOptions);

        Assert.Contains(result.Anomalies, a => a.Type == AnomalyType.ClockSkew);
        Assert.Equal(ReadingStatus.Suspect, result.Status);
    }

    [Fact]
    public void Assess_WhenClockSkewWithinThreshold_NoClockSkewAnomaly()
    {
        // Dispositivo 4 min atrás do servidor → 240s < 300s
        var capturedAt = BaseTime;
        var receivedAt = BaseTime.AddSeconds(240);
        var ctx = new ReadingAssessmentContext(
            PreviousValue: null,
            PreviousCapturedAt: null,
            PreviousReadingType: null,
            CurrentValue: 100m,
            CurrentCapturedAt: capturedAt,
            CurrentReadingType: ReadingType.Opening,
            ReceivedAtServer: receivedAt,
            IsPotentialDuplicate: false);

        var result = _policy.Assess(ctx, DefaultOptions);

        Assert.DoesNotContain(result.Anomalies, a => a.Type == AnomalyType.ClockSkew);
    }

    [Theory]
    [InlineData(299, false)] // 1s abaixo do limite
    [InlineData(300, false)] // exatamente no limite → dentro
    [InlineData(301, true)]  // 1s acima do limite → anomalia
    public void Assess_ClockSkewBoundary(int skewSeconds, bool expectAnomaly)
    {
        var capturedAt = BaseTime;
        var receivedAt = BaseTime.AddSeconds(skewSeconds);
        var ctx = new ReadingAssessmentContext(
            PreviousValue: null,
            PreviousCapturedAt: null,
            PreviousReadingType: null,
            CurrentValue: 100m,
            CurrentCapturedAt: capturedAt,
            CurrentReadingType: ReadingType.Opening,
            ReceivedAtServer: receivedAt,
            IsPotentialDuplicate: false);

        var result = _policy.Assess(ctx, DefaultOptions);

        if (expectAnomaly)
        {
            Assert.Contains(result.Anomalies, a => a.Type == AnomalyType.ClockSkew);
        }
        else
        {
            Assert.DoesNotContain(result.Anomalies, a => a.Type == AnomalyType.ClockSkew);
        }
    }

    // ────────────────────────────────────────────────
    // Casos: duplicata potencial
    // ────────────────────────────────────────────────

    [Fact]
    public void Assess_WhenIsPotentialDuplicate_GeneratesDuplicateReadingAnomaly()
    {
        var ctx = Context(currentValue: 100m, isPotentialDuplicate: true);
        var result = _policy.Assess(ctx, DefaultOptions);

        Assert.Contains(result.Anomalies, a => a.Type == AnomalyType.DuplicateReading);
        Assert.Equal(ReadingStatus.Suspect, result.Status);
    }

    [Fact]
    public void Assess_WhenNotDuplicate_NoDuplicateAnomaly()
    {
        var ctx = Context(currentValue: 100m, isPotentialDuplicate: false);
        var result = _policy.Assess(ctx, DefaultOptions);

        Assert.DoesNotContain(result.Anomalies, a => a.Type == AnomalyType.DuplicateReading);
    }

    // ────────────────────────────────────────────────
    // Casos: virada de dia (CLOSING → OPENING)
    // ────────────────────────────────────────────────

    [Fact]
    public void Assess_WhenDayTransitionExceedsTolerance_GeneratesAnomaly()
    {
        // CLOSING em 1000h, OPENING em 1001h: delta = 1h > 0.5h de tolerância
        var ctx = new ReadingAssessmentContext(
            PreviousValue: 1000m,
            PreviousCapturedAt: BaseTime,
            PreviousReadingType: ReadingType.Closing,
            CurrentValue: 1001m,
            CurrentCapturedAt: BaseTime.AddHours(8),
            CurrentReadingType: ReadingType.Opening,
            ReceivedAtServer: BaseTime.AddHours(8),
            IsPotentialDuplicate: false);

        var result = _policy.Assess(ctx, DefaultOptions);

        Assert.Contains(result.Anomalies, a => a.Type == AnomalyType.DayTransitionMismatch);
    }

    [Fact]
    public void Assess_WhenDayTransitionWithinTolerance_NoMismatchAnomaly()
    {
        // CLOSING em 1000h, OPENING em 1000.4h: delta = 0.4h < 0.5h de tolerância
        var ctx = new ReadingAssessmentContext(
            PreviousValue: 1000m,
            PreviousCapturedAt: BaseTime,
            PreviousReadingType: ReadingType.Closing,
            CurrentValue: 1000.4m,
            CurrentCapturedAt: BaseTime.AddHours(8),
            CurrentReadingType: ReadingType.Opening,
            ReceivedAtServer: BaseTime.AddHours(8),
            IsPotentialDuplicate: false);

        var result = _policy.Assess(ctx, DefaultOptions);

        Assert.DoesNotContain(result.Anomalies, a => a.Type == AnomalyType.DayTransitionMismatch);
    }

    [Theory]
    [InlineData(0.49, false)]
    [InlineData(0.50, false)]
    [InlineData(0.51, true)]
    [InlineData(-0.49, false)]
    [InlineData(-0.50, false)]
    [InlineData(-0.51, true)]
    public void Assess_DayTransitionToleranceBoundary(
        double readingDelta,
        bool expectMismatch)
    {
        var context = new ReadingAssessmentContext(
            PreviousValue: 1000m,
            PreviousCapturedAt: BaseTime,
            PreviousReadingType: ReadingType.Closing,
            CurrentValue: 1000m + (decimal)readingDelta,
            CurrentCapturedAt: BaseTime.AddHours(8),
            CurrentReadingType: ReadingType.Opening,
            ReceivedAtServer: BaseTime.AddHours(8),
            IsPotentialDuplicate: false);

        var result = _policy.Assess(context, DefaultOptions);

        Assert.Equal(
            expectMismatch,
            result.Anomalies.Any(anomaly => anomaly.Type == AnomalyType.DayTransitionMismatch));
    }

    // ────────────────────────────────────────────────
    // Casos: múltiplas anomalias simultâneas
    // ────────────────────────────────────────────────

    [Fact]
    public void Assess_WhenMultipleAnomalies_AllAreReported()
    {
        // Leitura regressiva + duplicata potencial
        var ctx = Context(previousValue: 100m, currentValue: 90m, elapsedHours: 1, isPotentialDuplicate: true);
        var result = _policy.Assess(ctx, DefaultOptions);

        Assert.Equal(ReadingStatus.Suspect, result.Status);
        Assert.True(result.Anomalies.Count >= 2);
        Assert.Contains(result.Anomalies, a => a.Type == AnomalyType.ReadingDecrease);
        Assert.Contains(result.Anomalies, a => a.Type == AnomalyType.DuplicateReading);
    }

    // ────────────────────────────────────────────────
    // Casos: UTC
    // ────────────────────────────────────────────────

    [Fact]
    public void Assess_WhenTimesInDifferentTimezones_NormalizesToUtcCorrectly()
    {
        // Mesmo instante, mas expresso em fusos horários diferentes
        var utc = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);
        var brasilia = new DateTimeOffset(2026, 10, 6, 7, 0, 0, TimeSpan.FromHours(-3));

        Assert.Equal(utc.ToUniversalTime(), brasilia.ToUniversalTime()); // mesma hora

        var ctx = new ReadingAssessmentContext(
            PreviousValue: 100m,
            PreviousCapturedAt: utc.AddHours(-2),
            PreviousReadingType: ReadingType.Intermediate,
            CurrentValue: 101m,
            CurrentCapturedAt: brasilia, // 10h UTC
            CurrentReadingType: ReadingType.Intermediate,
            ReceivedAtServer: utc,       // 10h UTC
            IsPotentialDuplicate: false);

        var result = _policy.Assess(ctx, DefaultOptions);

        // 2h transcorridas, aumento de 1h → plausível (2h * 1.0 + 0.25h = 2.25h)
        Assert.DoesNotContain(result.Anomalies, a => a.Type == AnomalyType.ImpossibleHourIncrease);
        // Dispositivo e servidor estão no mesmo instante → sem clock skew
        Assert.DoesNotContain(result.Anomalies, a => a.Type == AnomalyType.ClockSkew);
    }

    // ────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────

    private static ReadingAssessmentContext Context(
        decimal currentValue = 100m,
        decimal? previousValue = null,
        double elapsedHours = 0,
        bool isPotentialDuplicate = false,
        ReadingType previousReadingType = ReadingType.Intermediate,
        ReadingType currentReadingType = ReadingType.Intermediate)
    {
        DateTimeOffset? previousCapturedAt = previousValue.HasValue
            ? BaseTime.AddHours(-elapsedHours)
            : null;

        return new ReadingAssessmentContext(
            PreviousValue: previousValue,
            PreviousCapturedAt: previousCapturedAt,
            PreviousReadingType: previousValue.HasValue ? previousReadingType : null,
            CurrentValue: currentValue,
            CurrentCapturedAt: BaseTime,
            CurrentReadingType: currentReadingType,
            ReceivedAtServer: BaseTime,
            IsPotentialDuplicate: isPotentialDuplicate);
    }
}
