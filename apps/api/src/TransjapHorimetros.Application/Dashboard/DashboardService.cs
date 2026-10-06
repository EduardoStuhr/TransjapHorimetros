using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.Dashboard;

public sealed class DashboardService(
    IMachineRepository machineRepository,
    IReadingRepository readingRepository,
    IAnomalyRepository anomalyRepository,
    TimeProvider timeProvider) : IDashboardService
{
    public async Task<DashboardSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var startOfDay = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var totalMachines = await machineRepository.CountAsync(cancellationToken);
        var updatedToday = await readingRepository.CountDistinctMachinesReceivedSinceAsync(
            startOfDay,
            cancellationToken);
        var pending = await readingRepository.CountByStatusAsync(ReadingStatus.Pending, cancellationToken);
        var suspect = await readingRepository.CountByStatusAsync(ReadingStatus.Suspect, cancellationToken);
        var alerts = await anomalyRepository.CountOpenAsync(null, cancellationToken);

        return new DashboardSummaryResponse(
            totalMachines,
            updatedToday,
            0,
            false,
            "Métrica neutra: ainda não existe regra operacional que exija leitura diária para toda a frota.",
            pending,
            suspect,
            alerts);
    }
}
