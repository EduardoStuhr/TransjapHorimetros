namespace TransjapHorimetros.Application.QrCodes;

public interface IMachineQrCodeService
{
    Task<QrResolutionResponse> ResolveAsync(string value, CancellationToken cancellationToken);

    Task<IReadOnlyList<MachineQrCodeResponse>> GetHistoryAsync(
        Guid machineId,
        CancellationToken cancellationToken);

    Task<MachineQrCodeResponse> IssueAsync(
        Guid machineId,
        IssueMachineQrCodeRequest request,
        string correlationId,
        CancellationToken cancellationToken);

    Task DeactivateActiveAsync(
        Guid machineId,
        string correlationId,
        CancellationToken cancellationToken);
}
