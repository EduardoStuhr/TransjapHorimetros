namespace TransjapHorimetros.Application.Dashboard;

public sealed record DashboardSummaryResponse(
    int TotalMachines,
    int UpdatedToday,
    int WithoutReading,
    bool WithoutReadingIsDefined,
    string WithoutReadingDefinition,
    int PendingReadings,
    int SuspectReadings,
    int Alerts);
