using Microsoft.Extensions.Logging.Abstractions;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.PhotoEvidence;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.UnitTests;

public sealed class PhotoEvidenceServiceTests
{
    [Fact]
    public async Task UploadAsync_SameReadingAndHash_ReturnsExistingWithoutSecondUpload()
    {
        var context = CreateContext();
        var first = await UploadAsync(context, Guid.NewGuid());
        var second = await UploadAsync(context, Guid.NewGuid());

        Assert.False(first.IsExisting);
        Assert.True(second.IsExisting);
        Assert.Equal(first.Evidence.Id, second.Evidence.Id);
        Assert.Equal(1, context.Storage.UploadCount);
    }

    [Fact]
    public async Task UploadAsync_SameClientEventId_ReturnsExistingBeforeReadingFileAgain()
    {
        var context = CreateContext();
        var clientEventId = Guid.NewGuid();
        var first = await UploadAsync(context, clientEventId);

        var second = await context.Service.UploadAsync(
            context.Reading.Id,
            new UploadPhotoEvidenceRequest(
                new MemoryStream(),
                "ignored.jpg",
                "image/jpeg",
                null,
                "device-1",
                clientEventId),
            "test-correlation",
            CancellationToken.None);

        Assert.True(second.IsExisting);
        Assert.Equal(first.Evidence.Id, second.Evidence.Id);
        Assert.Equal(1, context.Storage.UploadCount);
    }

    private static async Task<UploadPhotoEvidenceResult> UploadAsync(
        TestContext context,
        Guid clientEventId) =>
        await context.Service.UploadAsync(
            context.Reading.Id,
            new UploadPhotoEvidenceRequest(
                new MemoryStream(PhotoFileValidatorTests.CreateJpeg()),
                "../../proof.jpg",
                "image/jpeg",
                DateTimeOffset.UtcNow,
                "device-1",
                clientEventId),
            "test-correlation",
            CancellationToken.None);

    private static TestContext CreateContext()
    {
        var reading = new HourMeterReading(
            Guid.NewGuid(),
            null,
            100m,
            ReadingType.Intermediate,
            ReadingStatus.Validated,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            Guid.NewGuid());
        var readingRepository = new ReadingRepositoryFake(reading);
        var evidenceRepository = new PhotoEvidenceRepositoryFake();
        var storage = new PhotoStorageFake();
        var unitOfWork = new UnitOfWorkFake();
        var options = new PhotoEvidenceOptions { MaximumFileSizeBytes = 1024 };
        var service = new PhotoEvidenceService(
            readingRepository,
            evidenceRepository,
            storage,
            new AuditLogRepositoryFake(),
            unitOfWork,
            new CurrentUserFake(),
            new PhotoFileValidator(options),
            TimeProvider.System,
            NullLogger<PhotoEvidenceService>.Instance);
        return new TestContext(service, reading, storage);
    }

    private sealed record TestContext(
        PhotoEvidenceService Service,
        HourMeterReading Reading,
        PhotoStorageFake Storage);

    private sealed class PhotoEvidenceRepositoryFake : IPhotoEvidenceRepository
    {
        private Domain.Entities.PhotoEvidence? _evidence;

        public Task<Domain.Entities.PhotoEvidence?> GetByReadingIdAsync(
            Guid readingId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_evidence?.ReadingId == readingId ? _evidence : null);

        public Task<Domain.Entities.PhotoEvidence?> GetByClientEventIdAsync(
            Guid clientEventId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_evidence?.ClientEventId == clientEventId ? _evidence : null);

        public void Add(Domain.Entities.PhotoEvidence photoEvidence) => _evidence = photoEvidence;
    }

    private sealed class PhotoStorageFake : IPhotoStorage
    {
        public int UploadCount { get; private set; }

        public string ProviderName => "InMemory";

        public string ContainerName => "photo-evidence-tests";

        public Task UploadAsync(
            string objectKey,
            Stream content,
            string contentType,
            CancellationToken cancellationToken)
        {
            UploadCount++;
            return Task.CompletedTask;
        }

        public Task<PhotoStorageContent> OpenReadAsync(
            string objectKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteIfExistsAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class ReadingRepositoryFake(HourMeterReading reading) : IReadingRepository
    {
        public Task<HourMeterReading?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == reading.Id ? reading : null);

        public Task<HourMeterReading?> GetByClientEventIdAsync(Guid clientEventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HourMeterReading?> GetLatestValidBeforeAsync(Guid machineId, DateTimeOffset capturedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<HourMeterReading>> GetLatestByMachineIdsAsync(IReadOnlyCollection<Guid> machineIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RepositoryPage<HourMeterReading>> GetPageAsync(ReadingQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RepositoryPage<HourMeterReading>> GetForMachineAsync(Guid machineId, int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> CountByStatusAsync(ReadingStatus status, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> CountDistinctMachinesReceivedSinceAsync(DateTimeOffset receivedSince, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistsSimilarAsync(Guid machineId, decimal value, DateTimeOffset capturedAt, Guid excludingClientEventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> CountActiveMachinesWithoutReadingSinceAsync(IReadOnlyCollection<Guid> activeMachineIds, DateTimeOffset capturedSince, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(HourMeterReading hourMeterReading) => throw new NotSupportedException();
    }

    private sealed class AuditLogRepositoryFake : IAuditLogRepository
    {
        public void Add(AuditLog auditLog)
        {
        }
    }

    private sealed class UnitOfWorkFake : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);

        public void ClearChanges()
        {
        }
    }

    private sealed class CurrentUserFake : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Email => "operator@tests.local";
        public string? FullName => "Operator Test";
        public IReadOnlyList<string> Roles => ["OPERATOR"];
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
