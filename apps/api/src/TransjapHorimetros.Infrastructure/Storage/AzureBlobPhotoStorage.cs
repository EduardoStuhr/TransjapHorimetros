using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.PhotoEvidence;

namespace TransjapHorimetros.Infrastructure.Storage;

public sealed class AzureBlobPhotoStorage(PhotoEvidenceOptions options) : IPhotoStorage
{
    private readonly Lazy<BlobContainerClient> _containerClient = new(
        () => CreateContainerClient(options),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string ProviderName => "AzureBlob";

    public string ContainerName => options.ContainerName;

    public async Task UploadAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var container = _containerClient.Value;
        await container.CreateIfNotExistsAsync(
            PublicAccessType.None,
            cancellationToken: cancellationToken);
        var blob = container.GetBlobClient(objectKey);

        try
        {
            await blob.UploadAsync(
                content,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                },
                cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 409)
        {
            throw new ConflictException("O objeto de evidência já existe no storage.");
        }
    }

    public async Task<PhotoStorageContent> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        var download = await _containerClient.Value
            .GetBlobClient(objectKey)
            .DownloadStreamingAsync(cancellationToken: cancellationToken);
        return new PhotoStorageContent(
            download.Value.Content,
            download.Value.Details.ContentType,
            download.Value.Details.ContentLength);
    }

    public async Task DeleteIfExistsAsync(
        string objectKey,
        CancellationToken cancellationToken) =>
        _ = await _containerClient.Value
            .GetBlobClient(objectKey)
            .DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                cancellationToken: cancellationToken);

    private static BlobContainerClient CreateContainerClient(PhotoEvidenceOptions options)
    {
        if (!string.Equals(options.Provider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Storage:Photos:Provider '{options.Provider}' não é suportado em produção.");
        }

        if (string.IsNullOrWhiteSpace(options.ContainerName))
        {
            throw new InvalidOperationException("Storage:Photos:ContainerName deve ser configurado.");
        }

        BlobServiceClient serviceClient;
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            serviceClient = new BlobServiceClient(options.ConnectionString);
        }
        else if (Uri.TryCreate(options.AccountUri, UriKind.Absolute, out var accountUri))
        {
            serviceClient = new BlobServiceClient(accountUri, new DefaultAzureCredential());
        }
        else
        {
            throw new InvalidOperationException(
                "Configure Storage:Photos:AccountUri para Managed Identity ou Storage:Photos:ConnectionString por secret.");
        }

        return serviceClient.GetBlobContainerClient(options.ContainerName);
    }
}
