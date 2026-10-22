using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.RegisteredDevices;
using TransjapHorimetros.Application.Security;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/devices")]
public sealed class RegisteredDevicesController(IRegisteredDeviceService service) : ControllerBase
{
    [HttpPost("registration")]
    [EnableRateLimiting("device-identity")]
    [ProducesResponseType(typeof(RegisteredDeviceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RegisteredDeviceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RegisteredDeviceResponse>> Register(
        RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var device = await service.RegisterAsync(request, HttpContext.TraceIdentifier, cancellationToken);
        return device.CreatedAt == device.UpdatedAt
            ? CreatedAtAction(nameof(Status), new { localDeviceId = device.LocalDeviceId }, device)
            : Ok(device);
    }

    [HttpGet("status")]
    [EnableRateLimiting("device-identity")]
    public Task<CurrentDeviceStatusResponse> Status(
        [FromQuery] string localDeviceId, CancellationToken cancellationToken) =>
        service.GetStatusAsync(localDeviceId, cancellationToken);

    [HttpPost("challenge")]
    [EnableRateLimiting("device-identity")]
    public Task<DeviceChallengeResponse> Challenge(
        DeviceChallengeRequest request, CancellationToken cancellationToken) =>
        service.CreateChallengeAsync(request, cancellationToken);

    [HttpPost("challenge/verify")]
    [EnableRateLimiting("device-identity")]
    public Task<DeviceSessionResponse> Verify(
        VerifyDeviceChallengeRequest request, CancellationToken cancellationToken) =>
        service.VerifyChallengeAsync(request, HttpContext.TraceIdentifier, cancellationToken);

    [HttpGet]
    [Authorize(Policy = Policies.CanManageDevices)]
    public Task<PagedResponse<RegisteredDeviceResponse>> GetAll(
        [FromQuery] RegisteredDeviceQuery query, CancellationToken cancellationToken) =>
        service.GetPageAsync(query, cancellationToken);

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = Policies.CanManageDevices)]
    public Task<IReadOnlyList<RegisteredDeviceAuditResponse>> GetHistory(
        Guid id, CancellationToken cancellationToken) => service.GetHistoryAsync(id, cancellationToken);

    [HttpPost("{id:guid}/authorize")]
    [Authorize(Policy = Policies.CanManageDevices)]
    public Task<RegisteredDeviceResponse> Authorize(Guid id, CancellationToken cancellationToken) =>
        service.AuthorizeAsync(id, HttpContext.TraceIdentifier, cancellationToken);

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = Policies.CanManageDevices)]
    public Task<RegisteredDeviceResponse> Revoke(Guid id, CancellationToken cancellationToken) =>
        service.RevokeAsync(id, HttpContext.TraceIdentifier, cancellationToken);

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Policies.CanManageDevices)]
    public Task<RegisteredDeviceResponse> Rename(
        Guid id, RenameDeviceRequest request, CancellationToken cancellationToken) =>
        service.RenameAsync(id, request, HttpContext.TraceIdentifier, cancellationToken);
}
