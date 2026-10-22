using TransjapHorimetros.Domain.Common;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Entities;

public sealed class HourMeterReading : AuditableEntity
{
    private HourMeterReading()
    {
    }

    internal HourMeterReading(
        Guid machineId,
        Guid? workSiteId,
        decimal value,
        ReadingType readingType,
        ReadingStatus status,
        DateTimeOffset capturedAtDevice,
        DateTimeOffset receivedAtServer,
        DateTimeOffset syncedAt,
        Guid clientEventId,
        MeterUnit? meterUnit = null,
        Guid? createdByUserId = null,
        string? createdByEmail = null,
        string? deviceId = null,
        Guid? previousReadingId = null,
        decimal? differenceFromPrevious = null,
        Guid? correctionOfReadingId = null,
        Guid? registeredDeviceId = null)
    {
        Id = Guid.NewGuid();
        MachineId = machineId;
        WorkSiteId = workSiteId;
        Value = value;
        ReadingType = readingType;
        Status = status;
        CapturedAtDevice = capturedAtDevice.ToUniversalTime();
        ReceivedAtServer = receivedAtServer.ToUniversalTime();
        SyncedAt = syncedAt.ToUniversalTime();
        ClientEventId = clientEventId;
        MeterUnit = meterUnit;
        CreatedByUserId = createdByUserId;
        CreatedByEmail = createdByEmail;
        DeviceId = deviceId;
        PreviousReadingId = previousReadingId;
        DifferenceFromPrevious = differenceFromPrevious;
        CorrectionOfReadingId = correctionOfReadingId;
        RegisteredDeviceId = registeredDeviceId;
        CreatedAt = SyncedAt;
        UpdatedAt = SyncedAt;
    }

    public Guid MachineId { get; private set; }

    public Machine? Machine { get; private set; }

    public Guid? WorkSiteId { get; private set; }

    public WorkSite? WorkSite { get; private set; }

    public decimal Value { get; private set; }

    public ReadingType ReadingType { get; private set; }

    public ReadingStatus Status { get; private set; }

    public DateTimeOffset CapturedAtDevice { get; private set; }

    public DateTimeOffset ReceivedAtServer { get; private set; }

    public DateTimeOffset SyncedAt { get; private set; }

    public Guid ClientEventId { get; private set; }

    public MeterUnit? MeterUnit { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public string? CreatedByEmail { get; private set; }
    public string? DeviceId { get; private set; }
    public Guid? PreviousReadingId { get; private set; }
    public decimal? DifferenceFromPrevious { get; private set; }
    public Guid? CorrectionOfReadingId { get; private set; }
    public Guid? RegisteredDeviceId { get; private set; }
    public RegisteredDevice? RegisteredDevice { get; private set; }
    public ICollection<HourMeterReading> Corrections { get; } = new List<HourMeterReading>();

    public static HourMeterReading CreateOperational(Machine machine, Guid? workSiteId, decimal value,
        ReadingType type, ReadingStatus initialStatus, DateTimeOffset capturedAt, DateTimeOffset receivedAt,
        Guid clientEventId, Guid actorUserId, string actorEmail, string? deviceId,
        HourMeterReading? previous = null, HourMeterReading? correctionOf = null)
        => CreateOperational(machine, workSiteId, value, type, initialStatus, capturedAt, receivedAt,
            clientEventId, actorUserId, actorEmail, deviceId, null, previous, correctionOf);

    public static HourMeterReading CreateOperational(Machine machine, Guid? workSiteId, decimal value,
        ReadingType type, ReadingStatus initialStatus, DateTimeOffset capturedAt, DateTimeOffset receivedAt,
        Guid clientEventId, Guid actorUserId, string actorEmail, string? deviceId, Guid? registeredDeviceId,
        HourMeterReading? previous = null, HourMeterReading? correctionOf = null)
    {
        if (machine.MeterUnit is null || !Enum.IsDefined(machine.MeterUnit.Value))
            throw new ArgumentException("Defina a unidade HOURS ou KM da máquina antes de registrar leituras.");
        if (value < 0 || value > 9_999_999_999.99m || decimal.Round(value, 2) != value)
            throw new ArgumentException("O valor deve ser não negativo, com até duas casas decimais e precisão decimal(12,2).");
        if (!Enum.IsDefined(type)) throw new ArgumentException("Tipo de leitura inválido.");
        if (clientEventId == Guid.Empty || actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(actorEmail)
            || actorEmail.Length > 320 || capturedAt == default || receivedAt == default)
            throw new ArgumentException("Evento, autoria e datas da leitura são obrigatórios.");
        Rules.ReadingStatusTransitionPolicy.EnsureInitial(initialStatus, type);
        deviceId = NormalizeDeviceId(deviceId);
        if (previous is not null && (previous.MachineId != machine.Id || previous.MeterUnit != machine.MeterUnit
            || previous.Status != ReadingStatus.Validated || previous.ReadingType == ReadingType.Correction
            || previous.CapturedAtDevice > capturedAt))
            throw new ArgumentException("A leitura anterior deve ser válida, da mesma máquina e unidade e anterior à captura.");
        if (type == ReadingType.Correction)
        {
            if (correctionOf is null || correctionOf.ReadingType == ReadingType.Correction
                || correctionOf.CorrectionOfReadingId.HasValue || correctionOf.MachineId != machine.Id
                || correctionOf.MeterUnit != machine.MeterUnit)
                throw new ArgumentException("A correção deve apontar diretamente para uma leitura original da mesma máquina e unidade, sem cadeia de correções.");
        }
        else if (correctionOf is not null) throw new ArgumentException("Somente CORRECTION pode referenciar uma leitura corrigida.");
        return new HourMeterReading(machine.Id, workSiteId, value, type, initialStatus, capturedAt, receivedAt,
            receivedAt, clientEventId, machine.MeterUnit, actorUserId, actorEmail.Trim(), deviceId,
            previous?.Id, previous is null ? null : value - previous.Value, correctionOf?.Id, registeredDeviceId);
    }

    public static string? NormalizeDeviceId(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return null;
        var normalized = deviceId.Trim();
        if (normalized.Length > 100 || normalized.Any(char.IsControl))
            throw new ArgumentException("DeviceId deve ter no máximo 100 caracteres e não pode conter caracteres de controle.");
        return normalized;
    }

    public ICollection<Anomaly> Anomalies { get; } = new List<Anomaly>();

    public PhotoEvidence? PhotoEvidence { get; private set; }
}
