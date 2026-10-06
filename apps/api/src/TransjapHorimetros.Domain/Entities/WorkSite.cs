using TransjapHorimetros.Domain.Common;

namespace TransjapHorimetros.Domain.Entities;

public sealed class WorkSite : AuditableEntity
{
    private WorkSite()
    {
    }

    public WorkSite(string name, string code, bool active, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Name = name.Trim();
        Code = code.Trim();
        Active = active;
        CreatedAt = now.ToUniversalTime();
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public bool Active { get; private set; }

    public ICollection<HourMeterReading> Readings { get; } = new List<HourMeterReading>();

    public void Update(string name, string code, bool active, DateTimeOffset now)
    {
        Name = name.Trim();
        Code = code.Trim();
        Active = active;
        UpdatedAt = now.ToUniversalTime();
    }
}
