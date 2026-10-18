using Microsoft.Extensions.Logging.Abstractions;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Anomalies;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.Machines;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Domain.Rules;

namespace TransjapHorimetros.UnitTests;

public sealed class ReadingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_WhenClientEventAlreadyExists_ReturnsExistingWithoutSaving()
    {
        var machine = new Machine(68, "Pipa Ford", MachineStatus.Active, Now, MeterUnit.Hours);
        var clientEventId = Guid.NewGuid();
        var existingReading = new HourMeterReading(
            machine.Id,
            null,
            100m,
            ReadingType.Opening,
            ReadingStatus.Validated,
            Now.AddHours(-1),
            Now.AddMinutes(-59),
            Now.AddMinutes(-59),
            clientEventId);
        typeof(HourMeterReading).GetProperty(nameof(HourMeterReading.Machine))!.SetValue(existingReading, machine);
        var machineRepository = new FakeMachineRepository(machine);
        var readingRepository = new FakeReadingRepository(existingReading);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(machineRepository, new FakeWorkSiteRepository(), readingRepository, unitOfWork);

        var result = await service.CreateAsync(
            ValidRequest(machine.Id, clientEventId),
            "test-correlation",
            CancellationToken.None);

        Assert.True(result.IsExisting);
        Assert.Equal(existingReading.Id, result.Reading.Id);
        Assert.Equal(0, unitOfWork.SaveCalls);
        Assert.Empty(readingRepository.AddedReadings);
    }

    [Fact]
    public async Task CreateAsync_WhenMachineDoesNotExist_ThrowsNotFound()
    {
        var service = CreateService(
            new FakeMachineRepository(null),
            new FakeWorkSiteRepository(),
            new FakeReadingRepository(),
            new FakeUnitOfWork());

        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.CreateAsync(
                ValidRequest(Guid.NewGuid(), Guid.NewGuid()),
                "test-correlation",
                CancellationToken.None));

        Assert.Equal("Máquina", exception.Entity);
    }

    [Fact]
    public async Task CreateAsync_WhenWorkSiteDoesNotExist_ThrowsNotFound()
    {
        var machine = new Machine(68, "Pipa Ford", MachineStatus.Active, Now, MeterUnit.Hours);
        var request = ValidRequest(machine.Id, Guid.NewGuid()) with { WorkSiteId = Guid.NewGuid() };
        var service = CreateService(
            new FakeMachineRepository(machine),
            new FakeWorkSiteRepository(),
            new FakeReadingRepository(),
            new FakeUnitOfWork());

        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.CreateAsync(request, "test-correlation", CancellationToken.None));

        Assert.Equal("Obra", exception.Entity);
    }

    private static ReadingService CreateService(
        IMachineRepository machineRepository,
        IWorkSiteRepository workSiteRepository,
        FakeReadingRepository readingRepository,
        IUnitOfWork unitOfWork) =>
        new(
            machineRepository,
            workSiteRepository,
            readingRepository,
            new FakeAnomalyRepository(),
            new FakeAuditLogRepository(),
            unitOfWork,
            new HourMeterReadingPolicy(),
            new HourMeterRulesOptions { ElapsedTimeToleranceHours = 0.25m },
            new FixedTimeProvider(Now),
            NullLogger<ReadingService>.Instance, new FakeCurrentUser());

    [Fact]
    public async Task CreateAsync_CapturesAuthorSnapshotDeviceAndServerCalculatedDelta()
    {
        var machine = new Machine(68, "Test", MachineStatus.Active, Now, MeterUnit.Hours);
        var previous = new HourMeterReading(machine.Id, null, 100, ReadingType.Intermediate,
            ReadingStatus.Validated, Now.AddHours(-2), Now.AddHours(-2), Now.AddHours(-2), Guid.NewGuid(), MeterUnit.Hours);
        var repository = new FakeReadingRepository(previousReading: previous);
        var uow = new FakeUnitOfWork();
        var service = CreateService(new FakeMachineRepository(machine), new FakeWorkSiteRepository(), repository, uow);
        var request = ValidRequest(machine.Id, Guid.NewGuid()) with { Value = 100.75m, DeviceId = "  device  " };
        var result = await service.CreateAsync(request, "test", default);
        Assert.False(result.IsExisting);
        Assert.Equal(MeterUnit.Hours, result.Reading.MeterUnit);
        Assert.Equal("operator@tests.local", result.Reading.CreatedByEmail);
        Assert.NotNull(result.Reading.CreatedByUserId);
        Assert.Equal("device", result.Reading.DeviceId);
        Assert.Equal(previous.Id, result.Reading.PreviousReadingId);
        Assert.Equal(0.75m, result.Reading.DifferenceFromPrevious);
        Assert.Equal(1, uow.SaveCalls);
    }

    [Fact]
    public async Task CreateAsync_MissingUnitDoesNotPersistNewReading()
    {
        var machine = new Machine(68, "Unknown", MachineStatus.Active, Now);
        var repository = new FakeReadingRepository(); var uow = new FakeUnitOfWork();
        var service = CreateService(new FakeMachineRepository(machine), new FakeWorkSiteRepository(), repository, uow);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.CreateAsync(ValidRequest(machine.Id, Guid.NewGuid()), "test", default));
        Assert.Empty(repository.AddedReadings); Assert.Equal(0, uow.SaveCalls);
    }

    private static CreateReadingRequest ValidRequest(Guid machineId, Guid clientEventId) =>
        new()
        {
            MachineId = machineId,
            Value = 100m,
            ReadingType = ReadingType.Opening,
            CapturedAtDevice = Now.AddHours(-1),
            ClientEventId = clientEventId,
        };

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Email => "operator@tests.local";
        public string? FullName => "Test Operator";
        public IReadOnlyList<string> Roles => ["OPERATOR"];
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeMachineRepository(Machine? machine) : IMachineRepository
    {
        public Task<Machine?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) =>
            Task.FromResult(machine?.Id == id ? machine : null);

        public Task<Machine?> GetByFleetNumberAsync(int fleetNumber, bool trackChanges, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> FleetNumberExistsAsync(int fleetNumber, Guid? excludingId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepositoryPage<Machine>> GetPageAsync(MachineQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> CountAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetActiveIdsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(Machine machineToAdd) => throw new NotSupportedException();
    }

    private sealed class FakeWorkSiteRepository(WorkSite? workSite = null) : IWorkSiteRepository
    {
        public Task<WorkSite?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) =>
            Task.FromResult(workSite?.Id == id ? workSite : null);

        public Task<IReadOnlyList<WorkSite>> GetAllAsync(bool? active, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(WorkSite workSiteToAdd) => throw new NotSupportedException();
    }

    private sealed class FakeReadingRepository(HourMeterReading? existingReading = null, HourMeterReading? previousReading = null) : IReadingRepository
    {
        public List<HourMeterReading> AddedReadings { get; } = [];

        public Task<HourMeterReading?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(existingReading?.Id == id ? existingReading : AddedReadings.SingleOrDefault(reading => reading.Id == id));

        public Task<HourMeterReading?> GetByClientEventIdAsync(Guid clientEventId, CancellationToken cancellationToken) =>
            Task.FromResult(existingReading?.ClientEventId == clientEventId ? existingReading : null);

        public Task<HourMeterReading?> GetLatestValidBeforeAsync(Guid machineId, DateTimeOffset capturedAt, CancellationToken cancellationToken, MeterUnit? meterUnit = null) =>
            Task.FromResult(previousReading);

        public Task<IReadOnlyList<HourMeterReading>> GetLatestByMachineIdsAsync(IReadOnlyCollection<Guid> machineIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepositoryPage<HourMeterReading>> GetPageAsync(ReadingQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepositoryPage<HourMeterReading>> GetForMachineAsync(Guid machineId, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> CountByStatusAsync(ReadingStatus status, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> CountDistinctMachinesReceivedSinceAsync(DateTimeOffset receivedSince, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ExistsSimilarAsync(
            Guid machineId,
            decimal value,
            DateTimeOffset capturedAt,
            Guid excludingClientEventId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<int> CountActiveMachinesWithoutReadingSinceAsync(
            IReadOnlyCollection<Guid> activeMachineIds,
            DateTimeOffset capturedSince,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(HourMeterReading reading) => AddedReadings.Add(reading);
    }

    private sealed class FakeAnomalyRepository : IAnomalyRepository
    {
        public Task<Anomaly?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepositoryPage<Anomaly>> GetPageAsync(AnomalyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> CountOpenAsync(Guid? machineId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(Anomaly anomaly)
        {
        }
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public void Add(AuditLog auditLog)
        {
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }

        public void ClearChanges()
        {
        }
    }
}
