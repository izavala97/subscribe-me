using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace suscribe_me.Services;

/// <summary>
/// Azure Blob Storage implementation for file storage
/// </summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(IConfiguration configuration, ILogger<AzureBlobStorageService> logger)
    {
        _logger = logger;
        
        var connectionString = configuration["Azure:BlobStorage:ConnectionString"]
            ?? throw new InvalidOperationException("Azure Blob Storage connection string not configured");
        var containerName = configuration["Azure:BlobStorage:ContainerName"] ?? "media";
        
        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType)
    {
        // Generate unique blob name to avoid collisions
        var extension = Path.GetExtension(fileName);
        var blobName = $"{Guid.NewGuid():N}{extension}";
        
        var blobClient = _containerClient.GetBlobClient(blobName);
        
        await blobClient.UploadAsync(content, new BlobHttpHeaders
        {
            ContentType = contentType
        });
        
        _logger.LogInformation("Uploaded blob {BlobName} ({ContentType})", blobName, contentType);
        
        return blobName;
    }

    public async Task<Stream?> DownloadAsync(string blobName)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        
        if (!await blobClient.ExistsAsync())
        {
            _logger.LogWarning("Blob {BlobName} not found", blobName);
            return null;
        }
        
        var response = await blobClient.DownloadStreamingAsync();
        return response.Value.Content;
    }

    public async Task DeleteAsync(string blobName)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();
        
        _logger.LogInformation("Deleted blob {BlobName}", blobName);
    }

    public Task<string?> GetUrlAsync(string blobName)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        
        if (!blobClient.Exists())
            return Task.FromResult<string?>(null);
        
        return Task.FromResult<string?>(blobClient.Uri.ToString());
    }
}
