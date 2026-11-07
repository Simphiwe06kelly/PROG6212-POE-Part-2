namespace RaceDay.Api.Services;

/// <summary>
/// Stores uploaded images and returns a public URL for them. The real implementation uses Azure Blob
/// Storage; the unit tests use an in-memory fake so they never need an Azure account.
/// </summary>
public interface IImageStorage
{
    /// <param name="content">The image bytes.</param>
    /// <param name="contentType">For example image/png.</param>
    /// <param name="blobName">Path inside the container, for example events/7/abc123.png.</param>
    /// <returns>The public URL of the stored image.</returns>
    Task<string> UploadAsync(Stream content, string contentType, string blobName, CancellationToken cancellationToken = default);
}
