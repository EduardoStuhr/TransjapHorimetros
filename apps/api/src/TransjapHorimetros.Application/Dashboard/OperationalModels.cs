using System.ComponentModel.DataAnnotations;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.Dashboard;

public sealed record ReviewRequest(
    bool Approved,
    [Required, StringLength(1000, MinimumLength = 3)] string Reason);

public sealed record AnomalyReviewRequest(
    TransjapHorimetros.Domain.Enums.AnomalyStatus ExpectedStatus,
    TransjapHorimetros.Domain.Enums.AnomalyStatus Status,
    [Required, StringLength(1000, MinimumLength = 3)] string Reason);

public sealed record OperationalGroup(string Label, int Count);
public sealed record StaleMachine(int FleetNumber, string Model, DateTimeOffset? LastReceivedAt);
public sealed record OperationalSummary(
    int TotalMachines, int ActiveMachines, int Readings, int ReceivedToday,
    int PendingReview, int SuspectReadings, int OpenAnomalies,
    IReadOnlyList<OperationalGroup> ByWorkSite, IReadOnlyList<OperationalGroup> ByDay,
    IReadOnlyList<StaleMachine> OldestMachines);

public interface IOperationalService
{
    Task<UtilizationReport> GetUtilizationAsync(ReadingQuery query, CancellationToken ct);
    Task<OperationalSummary> GetSummaryAsync(ReadingQuery query, CancellationToken ct);
    Task ReviewAnomalyAsync(Guid id, AnomalyReviewRequest request, string correlationId, CancellationToken ct);
    Task<ReadingReview?> GetReviewAsync(Guid id, CancellationToken ct);
    Task<ReadingReview> ReviewAsync(Guid id, ReviewRequest request, string correlationId, CancellationToken ct);
}
