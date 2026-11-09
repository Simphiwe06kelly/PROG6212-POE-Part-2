using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

/// <summary>
/// Azure Blob Storage implementation. Settings come from configuration:
///   AzureBlob:ConnectionString  (never commit a real key - use user-secrets or an environment variable)
///   AzureBlob:ContainerName     (default "raceday-images")
/// The container is created on first use with public read access for blobs, so the image URL can be
/// shown directly in an &lt;img&gt; tag. The default connection string targets the local Azurite emulator.
/// </summary>
public class BlobImageStorage : IImageStorage
{
    private readonly Lazy<BlobContainerClient> _container;

    public BlobImageStorage(IConfiguration configuration)
    {
        // Lazy so that a missing setting only affects uploads, not every request that needs this service
        _container = new Lazy<BlobContainerClient>(() =>
        {
            var connectionString = configuration["AzureBlob:ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("AzureBlob:ConnectionString is not configured.");

            var containerName = configuration["AzureBlob:ContainerName"];
            if (string.IsNullOrWhiteSpace(containerName)) containerName = "raceday-images";

            return new BlobContainerClient(connectionString, containerName);
        });
    }

    public async Task<string> UploadAsync(Stream content, string contentType, string blobName,
        CancellationToken cancellationToken = default)
    {
        var container = _container.Value;
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var blob = container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, cancellationToken);

        return blob.Uri.ToString();
    }
}
