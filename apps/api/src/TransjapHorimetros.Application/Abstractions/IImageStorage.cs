namespace TransjapHorimetros.Application.Abstractions;

public interface IImageStorage
{
    Task<string> StoreAsync(
        Stream content,
        string contentType,
        string fileName,
        CancellationToken cancellationToken);
}
