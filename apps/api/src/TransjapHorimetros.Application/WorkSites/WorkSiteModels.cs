using System.ComponentModel.DataAnnotations;

namespace TransjapHorimetros.Application.WorkSites;

public sealed record CreateWorkSiteRequest
{
    [Required]
    [StringLength(160, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(40, MinimumLength = 1)]
    public string Code { get; init; } = string.Empty;

    public bool Active { get; init; } = true;
}

public sealed record UpdateWorkSiteRequest
{
    [Required]
    [StringLength(160, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(40, MinimumLength = 1)]
    public string Code { get; init; } = string.Empty;

    public bool Active { get; init; }
}

public sealed record WorkSiteResponse(
    Guid Id,
    string Name,
    string Code,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
