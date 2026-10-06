using System.Text.Json;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Application.WorkSites;

public sealed class WorkSiteService(
    IWorkSiteRepository workSiteRepository,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IWorkSiteService
{
    public async Task<IReadOnlyList<WorkSiteResponse>> GetAllAsync(
        bool? active,
        CancellationToken cancellationToken)
    {
        var workSites = await workSiteRepository.GetAllAsync(active, cancellationToken);
        return workSites.Select(ToResponse).ToArray();
    }

    public async Task<WorkSiteResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var workSite = await workSiteRepository.GetByIdAsync(id, false, cancellationToken)
            ?? throw new EntityNotFoundException("Obra", id);
        return ToResponse(workSite);
    }

    public async Task<WorkSiteResponse> CreateAsync(
        CreateWorkSiteRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        Validate(request.Name, request.Code);
        var now = timeProvider.GetUtcNow();
        var workSite = new WorkSite(request.Name, request.Code, request.Active, now);
        workSiteRepository.Add(workSite);
        auditLogRepository.Add(new AuditLog(
            AuditActions.WorkSiteCreated,
            nameof(WorkSite),
            workSite.Id.ToString(),
            null,
            Serialize(workSite),
            now,
            correlationId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(workSite);
    }

    public async Task<WorkSiteResponse> UpdateAsync(
        Guid id,
        UpdateWorkSiteRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        Validate(request.Name, request.Code);
        var workSite = await workSiteRepository.GetByIdAsync(id, true, cancellationToken)
            ?? throw new EntityNotFoundException("Obra", id);
        var oldValue = Serialize(workSite);
        var now = timeProvider.GetUtcNow();
        workSite.Update(request.Name, request.Code, request.Active, now);
        auditLogRepository.Add(new AuditLog(
            AuditActions.WorkSiteUpdated,
            nameof(WorkSite),
            workSite.Id.ToString(),
            oldValue,
            Serialize(workSite),
            now,
            correlationId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(workSite);
    }

    private static WorkSiteResponse ToResponse(WorkSite workSite) =>
        new(
            workSite.Id,
            workSite.Name,
            workSite.Code,
            workSite.Active,
            workSite.CreatedAt,
            workSite.UpdatedAt);

    private static void Validate(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ApplicationValidationException(nameof(name), "O nome da obra é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ApplicationValidationException(nameof(code), "O código da obra é obrigatório.");
        }
    }

    private static string Serialize(WorkSite workSite) =>
        JsonSerializer.Serialize(
            new { workSite.Id, workSite.Name, workSite.Code, workSite.Active },
            ApplicationJson.Options);
}
