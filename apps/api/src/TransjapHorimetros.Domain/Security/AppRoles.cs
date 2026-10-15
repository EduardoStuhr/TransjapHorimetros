namespace TransjapHorimetros.Domain.Security;

public static class AppRoles
{
    public const string Operator = "OPERATOR";
    public const string Supervisor = "SUPERVISOR";
    public const string Administrative = "ADMINISTRATIVE";
    public const string Manager = "MANAGER";
    public const string Admin = "ADMIN";

    public static readonly IReadOnlyList<string> All =
    [
        Operator,
        Supervisor,
        Administrative,
        Manager,
        Admin,
    ];

    public static bool IsValid(string? role) =>
        !string.IsNullOrWhiteSpace(role)
        && All.Contains(role.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase);
}
