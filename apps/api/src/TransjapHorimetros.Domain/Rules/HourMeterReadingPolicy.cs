using System.Globalization;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Rules;

public sealed class HourMeterReadingPolicy
{
    public ReadingAssessment Assess(
        ReadingAssessmentContext context,
        HourMeterRulesOptions options)
    {
        if (context.CurrentValue < 0m)
        {
            return ReadingAssessment.Rejected("O valor da leitura não pode ser negativo.");
        }

        var anomalies = new List<AnomalyDetection>();

        if (context.IsPotentialDuplicate)
        {
            anomalies.Add(Detect(
                AnomalyType.DuplicateReading,
                "Outra leitura da mesma máquina possui exatamente o mesmo valor e horário de captura."));
        }

        DetectClockSkew(context, options, anomalies);

        if (context.PreviousValue.HasValue && context.PreviousCapturedAt.HasValue)
        {
            DetectSequenceAnomalies(context, options, anomalies);
        }

        return anomalies.Count == 0
            ? ReadingAssessment.Validated
            : new ReadingAssessment(ReadingStatus.Suspect, anomalies);
    }

    private static void DetectClockSkew(
        ReadingAssessmentContext context,
        HourMeterRulesOptions options,
        ICollection<AnomalyDetection> anomalies)
    {
        var clockSkewSeconds = Math.Abs(
            (context.ReceivedAtServer.ToUniversalTime() - context.CurrentCapturedAt.ToUniversalTime())
            .TotalSeconds);

        if (clockSkewSeconds > options.ClockSkewToleranceSeconds)
        {
            anomalies.Add(Detect(
                AnomalyType.ClockSkew,
                $"O horário do dispositivo diverge {Format((decimal)clockSkewSeconds)} segundos do horário de recebimento do servidor."));
        }
    }

    private static void DetectSequenceAnomalies(
        ReadingAssessmentContext context,
        HourMeterRulesOptions options,
        ICollection<AnomalyDetection> anomalies)
    {
        var previousValue = context.PreviousValue!.Value;
        var previousCapturedAt = context.PreviousCapturedAt!.Value;
        var readingDelta = context.CurrentValue - previousValue;

        if (readingDelta < 0m)
        {
            anomalies.Add(Detect(
                AnomalyType.ReadingDecrease,
                $"A leitura {Format(context.CurrentValue)} é menor que a leitura válida anterior {Format(previousValue)}."));
        }
        else
        {
            var elapsedHours = Math.Max(
                0m,
                (decimal)(context.CurrentCapturedAt.ToUniversalTime() - previousCapturedAt.ToUniversalTime()).TotalHours);
            var elapsedLimit =
                (elapsedHours * options.MaxPlausibleHoursPerElapsedHour)
                + options.ElapsedTimeToleranceHours;
            var elapsedDays = Math.Max(1m, Math.Ceiling(elapsedHours / 24m));
            var dailyLimit =
                (elapsedDays * options.MaxPlausibleDailyHours)
                + options.ElapsedTimeToleranceHours;
            var maximumPlausibleIncrease = Math.Min(elapsedLimit, dailyLimit);

            if (readingDelta > maximumPlausibleIncrease)
            {
                anomalies.Add(Detect(
                    AnomalyType.ImpossibleHourIncrease,
                    $"O aumento de {Format(readingDelta)} excede o limite plausível de {Format(maximumPlausibleIncrease)} para {Format(elapsedHours)} horas transcorridas."));
            }
        }

        if (context.PreviousReadingType == ReadingType.Closing
            && context.CurrentReadingType == ReadingType.Opening
            && Math.Abs(readingDelta) > options.DayTransitionToleranceHours)
        {
            anomalies.Add(Detect(
                AnomalyType.DayTransitionMismatch,
                $"A diferença entre o fechamento e a abertura seguinte é de {Format(Math.Abs(readingDelta))}, acima da tolerância de {Format(options.DayTransitionToleranceHours)}."));
        }
    }

    private static AnomalyDetection Detect(AnomalyType type, string description) =>
        new(type, AnomalySeverityPolicy.For(type), description);

    private static string Format(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
