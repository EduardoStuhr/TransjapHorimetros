using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Security;

namespace TransjapHorimetros.Application.PhotoEvidence;

public static class EvidenceAccess
{
    public static bool IsSupervisor(ICurrentUser user) => user.Roles.Any(role => role is
        AppRoles.Admin or AppRoles.Manager or AppRoles.Supervisor or AppRoles.Administrative);

    public static void EnsureCanRead(HourMeterReading reading, ICurrentUser user)
    {
        if (!user.IsAuthenticated) throw new UnauthorizedException("Autenticação obrigatória.");
        if (!IsSupervisor(user) && (user.UserId is null || reading.CreatedByUserId != user.UserId))
            throw new EntityNotFoundException("Evidência fotográfica", reading.Id);
    }
}
