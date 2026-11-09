using RaceDay.Api.Services;

namespace RaceDay.Api.Tests.Helpers;

/// <summary>Pretends to be Azure Blob Storage: keeps the upload details in memory and returns a fake URL.</summary>
public class FakeImageStorage : IImageStorage
{
    private readonly List<(string BlobName, string ContentType, long Length)> _uploads = new();

    public IReadOnlyList<(string BlobName, string ContentType, long Length)> Uploads
    {
        get { lock (_uploads) { return _uploads.ToList(); } }
    }

    public async Task<string> UploadAsync(Stream content, string contentType, string blobName,
        CancellationToken cancellationToken = default)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);

        lock (_uploads)
        {
            _uploads.Add((blobName, contentType, copy.Length));
        }

        return $"https://fake-storage.test/raceday-images/{blobName}";
    }
}
