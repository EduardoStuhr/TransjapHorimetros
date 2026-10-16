namespace TransjapHorimetros.Application.Abstractions;

public interface IPhotoStorage
{
    string ProviderName { get; }

    string ContainerName { get; }

    Task UploadAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);

    Task<PhotoStorageContent> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken);

    Task DeleteIfExistsAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed record PhotoStorageContent(
    Stream Content,
    string ContentType,
    long ContentLength);
