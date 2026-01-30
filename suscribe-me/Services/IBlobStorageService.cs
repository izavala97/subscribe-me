namespace suscribe_me.Services;

/// <summary>
/// Service for storing and retrieving files (images, videos, etc.)
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Upload a file and return its URL
    /// </summary>
    Task<string> UploadAsync(Stream content, string fileName, string contentType);
    
    /// <summary>
    /// Download a file by name
    /// </summary>
    Task<Stream?> DownloadAsync(string blobName);
    
    /// <summary>
    /// Delete a file by name
    /// </summary>
    Task DeleteAsync(string blobName);
    
    /// <summary>
    /// Get a URL for a blob (may be a SAS URL for private blobs)
    /// </summary>
    Task<string?> GetUrlAsync(string blobName);
}
