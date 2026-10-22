using TransjapHorimetros.Application.Common;

namespace TransjapHorimetros.Application.RegisteredDevices;

public interface IRegisteredDeviceService
{
    Task<RegisteredDeviceResponse> RegisterAsync(RegisterDeviceRequest request, string correlationId, CancellationToken cancellationToken);
    Task<CurrentDeviceStatusResponse> GetStatusAsync(string localDeviceId, CancellationToken cancellationToken);
    Task<PagedResponse<RegisteredDeviceResponse>> GetPageAsync(RegisteredDeviceQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<RegisteredDeviceAuditResponse>> GetHistoryAsync(Guid id, CancellationToken cancellationToken);
    Task<RegisteredDeviceResponse> AuthorizeAsync(Guid id, string correlationId, CancellationToken cancellationToken);
    Task<RegisteredDeviceResponse> RevokeAsync(Guid id, string correlationId, CancellationToken cancellationToken);
    Task<RegisteredDeviceResponse> RenameAsync(Guid id, RenameDeviceRequest request, string correlationId, CancellationToken cancellationToken);
    Task<DeviceChallengeResponse> CreateChallengeAsync(DeviceChallengeRequest request, CancellationToken cancellationToken);
    Task<DeviceSessionResponse> VerifyChallengeAsync(VerifyDeviceChallengeRequest request, string correlationId, CancellationToken cancellationToken);
    Task<Guid?> ValidateDeviceSessionAsync(string? token, CancellationToken cancellationToken);
}
