using System.Collections.Concurrent;
using TransjapHorimetros.Application.Abstractions;

namespace TransjapHorimetros.IntegrationTests;

public sealed class InMemoryPhotoStorage : IPhotoStorage
{
    private readonly ConcurrentDictionary<string, StoredPhoto> _photos = new();

    public string ProviderName => "InMemory";

    public string ContainerName => "photo-evidence-tests";

    public async Task UploadAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (!_photos.TryAdd(objectKey, new StoredPhoto(buffer.ToArray(), contentType)))
        {
            throw new InvalidOperationException("Objeto duplicado no storage de teste.");
        }
    }

    public Task<PhotoStorageContent> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        if (!_photos.TryGetValue(objectKey, out var photo))
        {
            throw new FileNotFoundException("Evidência não encontrada no storage de teste.", objectKey);
        }

        return Task.FromResult(new PhotoStorageContent(
            new MemoryStream(photo.Content, writable: false),
            photo.ContentType,
            photo.Content.LongLength));
    }

    public Task DeleteIfExistsAsync(string objectKey, CancellationToken cancellationToken)
    {
        _photos.TryRemove(objectKey, out _);
        return Task.CompletedTask;
    }

    public void Clear() => _photos.Clear();

    private sealed record StoredPhoto(byte[] Content, string ContentType);
}
