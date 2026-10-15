using Microsoft.AspNetCore.Identity;

namespace TransjapHorimetros.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
    }

    public ApplicationUser(string email, string fullName)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        Id = Guid.NewGuid();
        Email = normalizedEmail;
        UserName = normalizedEmail;
        NormalizedEmail = normalizedEmail.ToUpperInvariant();
        NormalizedUserName = normalizedEmail.ToUpperInvariant();
        FullName = fullName.Trim();
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}
