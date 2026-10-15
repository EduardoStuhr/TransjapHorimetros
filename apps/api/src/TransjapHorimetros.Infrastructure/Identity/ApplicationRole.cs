using Microsoft.AspNetCore.Identity;

namespace TransjapHorimetros.Infrastructure.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
        Id = Guid.NewGuid();
        NormalizedName = roleName.Trim().ToUpperInvariant();
        ConcurrencyStamp = Guid.NewGuid().ToString();
    }
}
