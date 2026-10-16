using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using TransjapHorimetros.Application.Exceptions;

namespace TransjapHorimetros.Application.PhotoEvidence;

public sealed class PhotoFileValidator(PhotoEvidenceOptions options)
{
    private const int CopyBufferSize = 64 * 1024;

    public async Task<ValidatedPhotoFile> ValidateAndSpoolAsync(
        Stream source,
        string submittedContentType,
        CancellationToken cancellationToken)
    {
        if (!source.CanRead)
        {
            throw new BadRequestException("O arquivo enviado não pode ser lido.");
        }

        var maximumFileSize = options.MaximumFileSizeBytes > 0
            ? options.MaximumFileSizeBytes
            : PhotoEvidenceOptions.DefaultMaximumFileSizeBytes;
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"transjap-photo-{Guid.NewGuid():N}.tmp");
        var tempStream = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
            long totalBytes = 0;

            try
            {
                while (true)
                {
                    var read = await source.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    totalBytes += read;
                    if (totalBytes > maximumFileSize)
                    {
                        throw new PayloadTooLargeException(
                            $"A foto excede o limite de {maximumFileSize} bytes.");
                    }

                    hash.AppendData(buffer, 0, read);
                    await tempStream.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }

            if (totalBytes == 0)
            {
                throw new BadRequestException("O arquivo enviado está vazio.");
            }

            await tempStream.FlushAsync(cancellationToken);
            tempStream.Position = 0;
            var image = ImageFileInspector.Inspect(tempStream);
            var normalizedSubmittedType = submittedContentType.Trim().ToLowerInvariant();
            if (!string.Equals(
                    normalizedSubmittedType,
                    image.ContentType,
                    StringComparison.Ordinal))
            {
                throw new BadRequestException(
                    $"O conteúdo da imagem é {image.ContentType}, mas foi informado {submittedContentType}.");
            }

            tempStream.Position = 0;
            return new ValidatedPhotoFile(
                tempStream,
                totalBytes,
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
                image.ContentType,
                image.Extension,
                image.Width,
                image.Height);
        }
        catch
        {
            await tempStream.DisposeAsync();
            throw;
        }
    }

    public static string SanitizeOriginalFileName(string? fileName, string extension)
    {
        var leafName = Path.GetFileName(fileName ?? string.Empty).Trim();
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(leafName
            .Where(character =>
                !char.IsControl(character)
                && character != '/'
                && character != '\\'
                && !invalidCharacters.Contains(character))
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = $"photo.{extension}";
        }

        return sanitized.Length <= 255 ? sanitized : sanitized[..255];
    }

    public static string CreateStorageObjectKey(Guid readingId, Guid photoEvidenceId, string extension) =>
        $"readings/{readingId:D}/{photoEvidenceId:D}.{extension}";
}

public sealed class ValidatedPhotoFile(
    Stream content,
    long fileSizeBytes,
    string sha256,
    string contentType,
    string extension,
    int width,
    int height) : IAsyncDisposable
{
    public Stream Content { get; } = content;

    public long FileSizeBytes { get; } = fileSizeBytes;

    public string Sha256 { get; } = sha256;

    public string ContentType { get; } = contentType;

    public string Extension { get; } = extension;

    public int Width { get; } = width;

    public int Height { get; } = height;

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

internal static class ImageFileInspector
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    public static ImageFileInfo Inspect(Stream stream)
    {
        Span<byte> header = stackalloc byte[32];
        var bytesRead = ReadAtMost(stream, header);
        stream.Position = 0;

        if (bytesRead >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff)
        {
            return InspectJpeg(stream);
        }

        if (bytesRead >= 24 && header[..8].SequenceEqual(PngSignature))
        {
            return InspectPng(stream, header[..bytesRead]);
        }

        if (bytesRead >= 30
            && header[..4].SequenceEqual("RIFF"u8)
            && header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return InspectWebP(stream, header[..bytesRead]);
        }

        throw new BadRequestException("Formato de imagem inválido. Envie JPEG, PNG ou WEBP válido.");
    }

    private static ImageFileInfo InspectPng(Stream stream, ReadOnlySpan<byte> header)
    {
        if (!header.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new BadRequestException("Arquivo PNG corrompido ou sem cabeçalho IHDR.");
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4));
        if (width <= 0 || height <= 0 || stream.Length < 45)
        {
            throw new BadRequestException("Dimensões inválidas no arquivo PNG.");
        }

        Span<byte> trailer = stackalloc byte[12];
        stream.Position = stream.Length - trailer.Length;
        stream.ReadExactly(trailer);
        if (!trailer.Slice(4, 4).SequenceEqual("IEND"u8))
        {
            throw new BadRequestException("Arquivo PNG incompleto ou corrompido.");
        }

        return new ImageFileInfo("image/png", "png", width, height);
    }

    private static ImageFileInfo InspectJpeg(Stream stream)
    {
        if (stream.Length < 4)
        {
            throw new BadRequestException("Arquivo JPEG incompleto.");
        }

        stream.Position = stream.Length - 2;
        if (stream.ReadByte() != 0xff || stream.ReadByte() != 0xd9)
        {
            throw new BadRequestException("Arquivo JPEG incompleto ou corrompido.");
        }

        stream.Position = 2;
        Span<byte> lengthBytes = stackalloc byte[2];
        while (stream.Position < stream.Length - 1)
        {
            if (stream.ReadByte() != 0xff)
            {
                continue;
            }

            int marker;
            do
            {
                marker = stream.ReadByte();
            }
            while (marker == 0xff);

            if (marker < 0)
            {
                break;
            }

            if (marker is 0xd8 or 0xd9 || marker is >= 0xd0 and <= 0xd7)
            {
                continue;
            }

            if (stream.Read(lengthBytes) != 2)
            {
                break;
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes);
            if (segmentLength < 2 || stream.Position + segmentLength - 2 > stream.Length)
            {
                break;
            }

            if (IsStartOfFrame(marker))
            {
                Span<byte> dimensions = stackalloc byte[5];
                stream.ReadExactly(dimensions);
                var height = BinaryPrimitives.ReadUInt16BigEndian(dimensions.Slice(1, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(dimensions.Slice(3, 2));
                if (width == 0 || height == 0)
                {
                    break;
                }

                return new ImageFileInfo("image/jpeg", "jpg", width, height);
            }

            stream.Seek(segmentLength - 2, SeekOrigin.Current);
        }

        throw new BadRequestException("Arquivo JPEG corrompido ou sem dimensões válidas.");
    }

    private static ImageFileInfo InspectWebP(Stream stream, ReadOnlySpan<byte> header)
    {
        var declaredRiffSize = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(4, 4));
        if (declaredRiffSize + 8 != stream.Length)
        {
            throw new BadRequestException("Arquivo WEBP incompleto ou corrompido.");
        }

        var chunkType = header.Slice(12, 4);
        int width;
        int height;
        if (chunkType.SequenceEqual("VP8X"u8))
        {
            width = 1 + ReadUInt24LittleEndian(header.Slice(24, 3));
            height = 1 + ReadUInt24LittleEndian(header.Slice(27, 3));
        }
        else if (chunkType.SequenceEqual("VP8L"u8) && header[20] == 0x2f)
        {
            width = 1 + header[21] + ((header[22] & 0x3f) << 8);
            height = 1
                + ((header[22] & 0xc0) >> 6)
                + (header[23] << 2)
                + ((header[24] & 0x0f) << 10);
        }
        else if (chunkType.SequenceEqual("VP8 "u8)
            && header.Slice(23, 3).SequenceEqual(new byte[] { 0x9d, 0x01, 0x2a }))
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(26, 2)) & 0x3fff;
            height = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(28, 2)) & 0x3fff;
        }
        else
        {
            throw new BadRequestException("Arquivo WEBP corrompido ou com codificação não reconhecida.");
        }

        if (width <= 0 || height <= 0)
        {
            throw new BadRequestException("Dimensões inválidas no arquivo WEBP.");
        }

        return new ImageFileInfo("image/webp", "webp", width, height);
    }

    private static bool IsStartOfFrame(int marker) =>
        marker is >= 0xc0 and <= 0xc3
            or >= 0xc5 and <= 0xc7
            or >= 0xc9 and <= 0xcb
            or >= 0xcd and <= 0xcf;

    private static int ReadUInt24LittleEndian(ReadOnlySpan<byte> bytes) =>
        bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);

    private static int ReadAtMost(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}

internal sealed record ImageFileInfo(
    string ContentType,
    string Extension,
    int Width,
    int Height);
