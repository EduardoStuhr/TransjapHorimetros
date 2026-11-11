using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Security;
using TransjapHorimetros.Infrastructure.Persistence;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/audit")]
[Authorize(Policy = Policies.CanReviewAnomalies)]
public sealed class AuditController(TransjapDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, StringLength(100)] string? entityId = null, CancellationToken ct = default)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (entityId != null) query = query.Where(a => a.EntityId == entityId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * 20).Take(20)
            .Select(a => new { a.Id, a.Action, a.Entity, a.EntityId, a.CreatedAt, a.UserEmail }).ToListAsync(ct);
        return Ok(new { items, page, pageSize = 20, totalItems = total, totalPages = (int)Math.Ceiling(total / 20d) });
    }
}
