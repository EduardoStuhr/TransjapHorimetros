using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.RegisteredDevices;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterDeviceRequest(
    [property: Required, MaxLength(100)] string LocalDeviceId,
    [property: Required, MaxLength(100)] string DisplayName,
    [property: Required, MaxLength(50)] string Platform,
    [property: Required, MaxLength(1024)] string PublicKey,
    [property: Required, MaxLength(50)] string KeyAlgorithm);

public sealed record RegisteredDeviceQuery
{
    public RegisteredDeviceStatus? Status { get; init; }
    [MaxLength(100)] public string? Search { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RenameDeviceRequest([property: Required, MaxLength(100)] string DisplayName);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeviceChallengeRequest([property: Required, MaxLength(100)] string LocalDeviceId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VerifyDeviceChallengeRequest(Guid ChallengeId, [property: Required, MaxLength(200)] string Signature);

public sealed record RegisteredDeviceResponse(
    Guid Id,
    string LocalDeviceId,
    string DisplayName,
    string Platform,
    RegisteredDeviceStatus Status,
    string KeyAlgorithm,
    string PublicKeyFingerprint,
    Guid RequestedByUserId,
    string RequestedByEmail,
    DateTimeOffset RequestedAt,
    Guid? AuthorizedByUserId,
    string? AuthorizedByEmail,
    DateTimeOffset? AuthorizedAt,
    Guid? RevokedByUserId,
    string? RevokedByEmail,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DeviceChallengeResponse(Guid ChallengeId, string Payload, DateTimeOffset ExpiresAt);
public sealed record DeviceSessionResponse(string DeviceSessionToken, DateTimeOffset ExpiresAt, RegisteredDeviceResponse Device);
public sealed record RegisteredDeviceAuditResponse(string Action, DateTimeOffset CreatedAt, Guid? UserId, string? UserEmail);
