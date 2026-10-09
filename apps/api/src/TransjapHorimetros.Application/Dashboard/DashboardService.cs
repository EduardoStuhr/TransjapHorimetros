using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Domain.Rules;

namespace TransjapHorimetros.Application.Dashboard;

public sealed class DashboardService(
    IMachineRepository machineRepository,
    IReadingRepository readingRepository,
    IAnomalyRepository anomalyRepository,
    HourMeterRulesOptions rulesOptions,
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

        // RN-006 — máquina ativa sem leitura nos últimos N dias gera alerta (configurável)
        var thresholdDays = rulesOptions.MissingReadingThresholdDays;
        var thresholdDate = now.AddDays(-thresholdDays);
        var activeMachineIds = await machineRepository.GetActiveIdsAsync(cancellationToken);
        var withoutReading = await readingRepository.CountActiveMachinesWithoutReadingSinceAsync(
            activeMachineIds,
            thresholdDate,
            cancellationToken);

        var withoutReadingDefinition = thresholdDays == 1
            ? "Máquinas ativas sem leitura recebida hoje"
            : $"Máquinas ativas sem leitura nos últimos {thresholdDays} dias (padrão operacional provisório — pendente validação Transjap)";

        return new DashboardSummaryResponse(
            totalMachines,
            updatedToday,
            withoutReading,
            withoutReadingIsDefined: true,
            withoutReadingDefinition,
            pending,
            suspect,
            alerts);
    }
}
