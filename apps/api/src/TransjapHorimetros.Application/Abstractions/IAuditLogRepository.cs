using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.Abstractions;

public interface IAuditLogRepository
{
    void Add(AuditLog auditLog);
}
