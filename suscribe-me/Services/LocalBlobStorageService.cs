namespace suscribe_me.Services;

/// <summary>
/// Mock blob storage service for local development.
/// Stores files in the local file system (wwwroot/uploads).
/// </summary>
public class LocalBlobStorageService : IBlobStorageService
{
    private readonly string _uploadPath;
    private readonly ILogger<LocalBlobStorageService> _logger;

    public LocalBlobStorageService(IWebHostEnvironment environment, ILogger<LocalBlobStorageService> logger)
    {
        _logger = logger;
        _uploadPath = Path.Combine(environment.WebRootPath, "uploads");
        
        if (!Directory.Exists(_uploadPath))
        {
            Directory.CreateDirectory(_uploadPath);
            _logger.LogInformation("Created local upload directory: {Path}", _uploadPath);
        }
    }

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        var blobName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(_uploadPath, blobName);
        
        using var fileStream = new FileStream(filePath, FileMode.Create);
        await content.CopyToAsync(fileStream);
        
        _logger.LogInformation("💾 LOCAL: Saved file {BlobName} to {Path}", blobName, filePath);
        
        return blobName;
    }

    public Task<Stream?> DownloadAsync(string blobName)
    {
        var filePath = Path.Combine(_uploadPath, blobName);
        
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("💾 LOCAL: File {BlobName} not found", blobName);
            return Task.FromResult<Stream?>(null);
        }
        
        return Task.FromResult<Stream?>(new FileStream(filePath, FileMode.Open, FileAccess.Read));
    }

    public Task DeleteAsync(string blobName)
    {
        var filePath = Path.Combine(_uploadPath, blobName);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("💾 LOCAL: Deleted file {BlobName}", blobName);
        }
        
        return Task.CompletedTask;
    }

    public Task<string?> GetUrlAsync(string blobName)
    {
        var filePath = Path.Combine(_uploadPath, blobName);
        
        if (!File.Exists(filePath))
            return Task.FromResult<string?>(null);
        
        // Return relative URL for local files
        return Task.FromResult<string?>($"/uploads/{blobName}");
    }
}
