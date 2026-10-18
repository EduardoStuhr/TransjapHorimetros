using Microsoft.Extensions.Logging.Abstractions;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Anomalies;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.Ocr;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.UnitTests;

public sealed class PhotoOcrServiceTests
{
    [Theory]
    [InlineData("success", PhotoOcrStatus.Succeeded, null)]
    [InlineData("low", PhotoOcrStatus.LowConfidence, AnomalyType.LowOcrConfidence)]
    [InlineData("divergence", PhotoOcrStatus.Succeeded, AnomalyType.OcrOperatorDivergence)]
    [InlineData("empty", PhotoOcrStatus.NoValueDetected, null)]
    [InlineData("429", PhotoOcrStatus.Failed, null)]
    [InlineData("500", PhotoOcrStatus.Failed, null)]
    [InlineData("invalid", PhotoOcrStatus.Failed, null)]
    [InlineData("timeout", PhotoOcrStatus.Failed, null)]
    public async Task Process_RecordsOutcomeAndPreservesOperationalEvidence(string scenario, PhotoOcrStatus status, AnomalyType? type)
    {
        var context = Create(scenario);
        var response = await context.Service.ProcessAsync(context.Reading.Id, false, "correlation", default);
        Assert.Equal(status, response.Status);
        Assert.Equal(100, context.Reading.Value);
        Assert.Equal(PhotoEvidenceStatus.Active, context.Photo.Status);
        Assert.Single(context.Results.Items);
        Assert.Equal(type, context.Anomalies.Items.SingleOrDefault()?.Type);
        Assert.Equal(status == PhotoOcrStatus.Failed ? AuditActions.PhotoOcrFailed : AuditActions.PhotoOcrProcessed,
            context.Audits.Items.Single().Action);
        if (status == PhotoOcrStatus.Failed) Assert.NotNull(response.ErrorCode);
        if (type.HasValue) Assert.Equal(response.Id, context.Anomalies.Items.Single().OcrResultId);
    }

    [Fact]
    public async Task InitialRetryAndGets_DoNotRepeatProviderOrAnomalies()
    {
        var context = Create("divergence");
        var first = await context.Service.ProcessAsync(context.Reading.Id, false, "first", default);
        var retry = await context.Service.ProcessAsync(context.Reading.Id, false, "retry", default);
        await context.Service.GetLatestAsync(context.Reading.Id, default);
        await context.Service.GetHistoryAsync(context.Reading.Id, default);
        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(1, context.Provider.Calls);
        Assert.Single(context.Anomalies.Items);
    }

    [Fact]
    public async Task ManualProcessing_PreservesResultsAndAuditsActor()
    {
        var context = Create("success");
        await context.Service.ProcessAsync(context.Reading.Id, false, "initial", default);
        var response = await context.Service.ProcessAsync(context.Reading.Id, true, "manual", default);
        Assert.Equal(2, context.Results.Items.Count);
        Assert.False(context.Results.Items.Last().IsInitial);
        Assert.Equal(context.Results.Items.Last().Id, response.Id);
        Assert.NotNull(context.Audits.Items.Single(a => a.Action == AuditActions.PhotoOcrReprocessed).UserId);
    }

    [Fact]
    public async Task ManualProcessing_OperatorForbiddenBeforeProvider()
    {
        var context = Create("success", "OPERATOR");
        await Assert.ThrowsAsync<ForbiddenException>(() => context.Service.ProcessAsync(context.Reading.Id, true, "manual", default));
        Assert.Equal(0, context.Provider.Calls);
    }

    [Theory]
    [InlineData("429", 3)]
    [InlineData("500", 3)]
    [InlineData("invalid", 1)]
    public async Task Retries_OnlyTransientAndBounded(string scenario, int calls)
    {
        var context = Create(scenario, retries: 2);
        await context.Service.ProcessAsync(context.Reading.Id, false, "retry", default);
        Assert.Equal(calls, context.Provider.Calls);
        Assert.Single(context.Results.Items);
    }

    private static TestContext Create(string scenario, string role = "SUPERVISOR", int retries = 0)
    {
        var reading = new HourMeterReading(Guid.NewGuid(), null, 100, ReadingType.Intermediate,
            ReadingStatus.Validated, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Guid.NewGuid());
        var photo = new Domain.Entities.PhotoEvidence(Guid.NewGuid(), reading.Id, "Fake", "private", "key", "photo.png",
            "image/png", 20, new string('a', 64), null, DateTimeOffset.UtcNow, 1, 1, null, null, null, null);
        var photos = new PhotoEvidenceRepositoryFake(); photos.Add(photo);
        var results = new ResultRepository(); var anomalies = new Anomalies(); var audits = new Audits();
        var provider = new FakePhotoOcrProvider(scenario);
        var service = new PhotoOcrService(photos, new ReadingRepositoryFake(reading), results,
            new Storage(), provider, new MeterReadingExtractor(), new OcrOptions { TimeoutSeconds = 2, MaximumRetries = retries },
            anomalies, audits, new UnitOfWorkFake(), new User(role), TimeProvider.System, NullLogger<PhotoOcrService>.Instance);
        return new(service, reading, photo, results, anomalies, audits, provider);
    }

    private sealed record TestContext(PhotoOcrService Service, HourMeterReading Reading,
        Domain.Entities.PhotoEvidence Photo, ResultRepository Results, Anomalies Anomalies, Audits Audits, FakePhotoOcrProvider Provider);

    private sealed class FakePhotoOcrProvider(string scenario) : IPhotoOcrProvider
    {
        public string Name => "FakeTests";
        public int Calls { get; private set; }
        public async Task<OcrProviderResult> AnalyzeAsync(Stream content, string contentType, CancellationToken ct)
        {
            Calls++;
            if (scenario == "timeout") await Task.Delay(Timeout.Infinite, ct);
            if (scenario is "429" or "500") throw new OcrProviderException("OCR_HTTP_" + scenario, true);
            if (scenario == "invalid") throw new OcrProviderException("OCR_INVALID_RESPONSE");
            if (scenario == "empty") return new("unreadable", "test", []);
            var token = scenario == "divergence" ? "200" : "100";
            return new("HOURS " + token, "test", [new(token, scenario == "low" ? 0.2m : 0.95m)]);
        }
    }
    private sealed class ResultRepository : IPhotoOcrRepository
    {
        public List<PhotoOcrResult> Items { get; } = [];
        public Task<PhotoOcrResult?> StartAsync(PhotoOcrResult result, OcrOptions options, CancellationToken ct)
        {
            if (result.IsInitial && Items.Any(r => r.IsInitial)) return Task.FromResult<PhotoOcrResult?>(null);
            Items.Add(result); return Task.FromResult<PhotoOcrResult?>(result);
        }
        public Task<IReadOnlyList<PhotoOcrResult>> GetHistoryAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PhotoOcrResult>>(Items.OrderByDescending(r => r.CreatedAt).ToArray());
    }
    private sealed class Anomalies : IAnomalyRepository
    {
        public List<Anomaly> Items { get; } = [];
        public void Add(Anomaly a) => Items.Add(a);
        public Task<Anomaly?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<RepositoryPage<Anomaly>> GetPageAsync(AnomalyQuery q, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> CountOpenAsync(Guid? id, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Audits : IAuditLogRepository
    {
        public List<AuditLog> Items { get; } = [];
        public void Add(AuditLog audit) => Items.Add(audit);
    }
    private sealed class Storage : IPhotoStorage
    {
        public string ProviderName => "Fake";
        public string ContainerName => "private";
        public Task<PhotoStorageContent> OpenReadAsync(string key, CancellationToken ct) =>
            Task.FromResult(new PhotoStorageContent(new MemoryStream([1,2,3]), "image/png", 3));
        public Task UploadAsync(string key, Stream s, string type, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteIfExistsAsync(string key, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class User(string role) : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Email => "supervisor@tests.local";
        public string? FullName => "Test";
        public IReadOnlyList<string> Roles => [role];
        public bool IsAuthenticated => true;
        public bool IsInRole(string r) => role == r;
    }
    private sealed class PhotoEvidenceRepositoryFake : IPhotoEvidenceRepository
    {
        private Domain.Entities.PhotoEvidence? _evidence;
        public bool HideExisting { get; set; }

        public Task<Domain.Entities.PhotoEvidence?> GetByReadingIdAsync(
            Guid readingId,
            CancellationToken cancellationToken) =>
            Task.FromResult(!HideExisting && _evidence?.ReadingId == readingId ? _evidence : null);

        public Task<Domain.Entities.PhotoEvidence?> GetByClientEventIdAsync(
            Guid clientEventId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_evidence?.ClientEventId == clientEventId ? _evidence : null);

        public void Add(Domain.Entities.PhotoEvidence photoEvidence)
        {
            if (!HideExisting)
            {
                _evidence = photoEvidence;
            }
        }
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

    private sealed class UnitOfWorkFake : IUnitOfWork
    {
        public Action? OnSave { get; set; }
        public Action? OnClear { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            OnSave?.Invoke();
            return Task.FromResult(1);
        }

        public void ClearChanges()
        {
            OnClear?.Invoke();
        }
    }
}
