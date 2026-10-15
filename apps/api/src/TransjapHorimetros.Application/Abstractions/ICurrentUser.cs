namespace TransjapHorimetros.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Email { get; }

    string? FullName { get; }

    IReadOnlyList<string> Roles { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}
