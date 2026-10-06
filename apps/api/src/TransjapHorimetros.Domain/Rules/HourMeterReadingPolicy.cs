using System.Globalization;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Rules;

public sealed class HourMeterReadingPolicy
{
    public ReadingAssessment Assess(
        decimal? previousValue,
        DateTimeOffset? previousCapturedAt,
        decimal currentValue,
        DateTimeOffset currentCapturedAt,
        decimal elapsedTimeToleranceHours)
    {
        if (previousValue is null || previousCapturedAt is null)
        {
            return ReadingAssessment.Validated;
        }

        if (currentValue < previousValue.Value)
        {
            return new ReadingAssessment(
                ReadingStatus.Suspect,
                AnomalyType.ReadingDecrease,
                AnomalySeverity.Warning,
                $"A leitura {Format(currentValue)} é menor que a leitura válida anterior {Format(previousValue.Value)}.");
        }

        var elapsedHours = Math.Max(
            0m,
            (decimal)(currentCapturedAt.ToUniversalTime() - previousCapturedAt.Value.ToUniversalTime()).TotalHours);
        var readingDelta = currentValue - previousValue.Value;

        if (readingDelta > elapsedHours + elapsedTimeToleranceHours)
        {
            return new ReadingAssessment(
                ReadingStatus.Suspect,
                AnomalyType.ImpossibleHourIncrease,
                AnomalySeverity.Warning,
                $"O aumento de {Format(readingDelta)} h excede as {Format(elapsedHours)} h transcorridas mais a tolerância configurada.");
        }

        return ReadingAssessment.Validated;
    }

    private static string Format(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
