using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.Dashboard;

public sealed record UtilizationPoint(Guid Id, DateTimeOffset At, decimal Value, MeterUnit? Unit,
    Guid? Site, bool Accepted, bool Corrected);
public sealed record UtilizationInterval(Guid ReadingId, DateTimeOffset At, decimal Initial,
    decimal Final, decimal? Difference, string Reason, Guid? Site);

public static class Utilization
{
    // Captured time governs offline events. Equal timestamps provide no ordering evidence.
    public static IReadOnlyList<UtilizationInterval> Calculate(IEnumerable<UtilizationPoint> source,
        DateTimeOffset from, DateTimeOffset to)
    {
        var points = source.OrderBy(p => p.At).ThenBy(p => p.Id).ToArray();
        var counts = points.GroupBy(p => p.At).ToDictionary(g => g.Key, g => g.Count());
        var result = new List<UtilizationInterval>();
        for (var i = 0; i < points.Length; i++)
        {
            var end = points[i];
            if (end.At < from || end.At > to) continue;
            var start = i > 0 ? points[i - 1] : null;
            var reason = start is null ? "MISSING_INITIAL"
                : start.At < from ? "CROSSES_PERIOD"
                : counts[end.At] > 1 || counts[start.At] > 1 ? "DUPLICATE_TIME"
                : !start.Accepted || !end.Accepted ? "REVIEW_REQUIRED"
                : start.Corrected || end.Corrected ? "CORRECTION_BOUNDARY"
                : start.Unit != MeterUnit.Hours || end.Unit != MeterUnit.Hours ? "UNIT_NOT_HOURS"
                : end.Value < start.Value ? "NEGATIVE_OR_RESET"
                : "CALCULATED";
            result.Add(new(end.Id, end.At, start?.Value ?? end.Value, end.Value,
                reason == "CALCULATED" ? end.Value - start!.Value : null, reason,
                reason == "CALCULATED" && start!.Site == end.Site ? end.Site : null));
        }
        return result;
    }
}

public sealed record UtilizationRow(Guid MachineId, int FleetNumber, string Model,
    Guid ReadingId, DateTimeOffset At, decimal Initial, decimal Final, decimal? Hours,
    string Reason, Guid? WorkSiteId, string? WorkSite);
public sealed record UtilizationReport(decimal? Hours, int CalculatedIntervals, int UnavailableIntervals,
    IReadOnlyList<UtilizationRow> Items, int TotalItems, int Page, int PageSize);
