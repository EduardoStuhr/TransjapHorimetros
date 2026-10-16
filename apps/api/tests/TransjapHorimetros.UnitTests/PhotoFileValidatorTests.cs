using System.Security.Cryptography;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.PhotoEvidence;

namespace TransjapHorimetros.UnitTests;

public sealed class PhotoFileValidatorTests
{
    [Theory]
    [MemberData(nameof(ValidImages))]
    public async Task ValidateAndSpoolAsync_AcceptsSupportedImages(
        byte[] content,
        string contentType,
        int expectedWidth,
        int expectedHeight)
    {
        var validator = CreateValidator();

        await using var result = await validator.ValidateAndSpoolAsync(
            new MemoryStream(content),
            contentType,
            CancellationToken.None);

        Assert.Equal(contentType, result.ContentType);
        Assert.Equal(content.Length, result.FileSizeBytes);
        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            result.Sha256);
    }

    [Fact]
    public async Task ValidateAndSpoolAsync_RejectsEmptyFile()
    {
        var validator = CreateValidator();

        await Assert.ThrowsAsync<BadRequestException>(() => validator.ValidateAndSpoolAsync(
            new MemoryStream(),
            "image/jpeg",
            CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAndSpoolAsync_RejectsFileAboveConfiguredLimit()
    {
        var validator = CreateValidator(maximumFileSizeBytes: 10);

        await Assert.ThrowsAsync<PayloadTooLargeException>(() => validator.ValidateAndSpoolAsync(
            new MemoryStream(CreateJpeg()),
            "image/jpeg",
            CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAndSpoolAsync_RejectsFalseContentType()
    {
        var validator = CreateValidator();

        await Assert.ThrowsAsync<BadRequestException>(() => validator.ValidateAndSpoolAsync(
            new MemoryStream(CreatePng()),
            "image/jpeg",
            CancellationToken.None));
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/zip")]
    [InlineData("text/html")]
    [InlineData("image/svg+xml")]
    public async Task ValidateAndSpoolAsync_RejectsUnsupportedOrInvalidContent(string contentType)
    {
        var validator = CreateValidator();

        await Assert.ThrowsAsync<BadRequestException>(() => validator.ValidateAndSpoolAsync(
            new MemoryStream("not-an-image"u8.ToArray()),
            contentType,
            CancellationToken.None));
    }

    [Fact]
    public void CreateStorageObjectKey_NeverUsesSubmittedFileName()
    {
        var readingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var photoId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var key = PhotoFileValidator.CreateStorageObjectKey(readingId, photoId, "jpg");

        Assert.Equal(
            "readings/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222.jpg",
            key);
        Assert.DoesNotContain("..", key);
        Assert.DoesNotContain('\\', key);
    }

    public static TheoryData<byte[], string, int, int> ValidImages() => new()
    {
        { CreateJpeg(), "image/jpeg", 2, 1 },
        { CreatePng(), "image/png", 2, 1 },
        { CreateWebP(), "image/webp", 2, 1 },
    };

    internal static byte[] CreateJpeg() =>
    [
        0xff, 0xd8,
        0xff, 0xc0, 0x00, 0x11, 0x08,
        0x00, 0x01,
        0x00, 0x02,
        0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x00, 0x03, 0x11, 0x00,
        0xff, 0xd9,
    ];

    private static byte[] CreatePng()
    {
        var content = new byte[45];
        byte[] signature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        signature.CopyTo(content, 0);
        content[11] = 13;
        "IHDR"u8.CopyTo(content.AsSpan(12));
        content[19] = 2;
        content[23] = 1;
        "IEND"u8.CopyTo(content.AsSpan(37));
        return content;
    }

    private static byte[] CreateWebP()
    {
        var content = new byte[30];
        "RIFF"u8.CopyTo(content);
        BitConverter.GetBytes((uint)22).CopyTo(content, 4);
        "WEBP"u8.CopyTo(content.AsSpan(8));
        "VP8X"u8.CopyTo(content.AsSpan(12));
        BitConverter.GetBytes((uint)10).CopyTo(content, 16);
        content[24] = 1;
        return content;
    }

    private static PhotoFileValidator CreateValidator(long maximumFileSizeBytes = 1024) =>
        new(new PhotoEvidenceOptions { MaximumFileSizeBytes = maximumFileSizeBytes });
}
