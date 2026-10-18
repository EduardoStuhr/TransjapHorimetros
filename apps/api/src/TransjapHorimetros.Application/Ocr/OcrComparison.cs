namespace TransjapHorimetros.Application.Ocr;

public sealed record OcrComparison(decimal? AbsoluteDifference, decimal? PercentageDifference, bool? Matches, bool LowConfidence)
{
    public static OcrComparison Evaluate(decimal operatorValue, MeterExtraction extraction, OcrOptions options)
    {
        var low = extraction.Confidence.HasValue && extraction.Confidence < options.MinimumConfidence;
        if (!extraction.Value.HasValue) return new(null, null, null, low);
        var difference = Math.Abs(operatorValue - extraction.Value.Value);
        // With zero as denominator, only the absolute threshold is meaningful.
        decimal? percentage = operatorValue == 0 ? null : difference / Math.Abs(operatorValue) * 100;
        return new(difference, percentage,
            !(difference > options.MaximumDifferenceAbsolute || percentage > options.MaximumDifferencePercentage), low);
    }
}
