namespace PharmacyApiService.Application.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadAsync(Stream stream, string containerName, string blobName, string contentType, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string containerName, string blobName, CancellationToken ct = default);
    Task DeleteAsync(string containerName, string blobName, CancellationToken ct = default);
    Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken ct = default);

    /// <summary>
    /// Moves a blob between containers atomically.
    /// Local fake implementation uses File.Move; Azure implementation copies then deletes.
    /// </summary>
    Task MoveAsync(string sourceContainer, string sourceBlobName, string destinationContainer, string destinationBlobName, CancellationToken ct = default);

    /// <summary>
    /// Returns the addressable URL (Azure) or absolute file path (local fake) for a blob.
    /// Used by the prediction service to construct the datasetPath sent to the ML Service.
    /// </summary>
    string GetBlobUrl(string containerName, string blobName);

    /// <summary>
    /// Returns a pre-authenticated URL valid for the given duration.
    /// Azure: generates a SAS URL. Local fake: returns the absolute file path.
    /// Used to give the ML Service temporary read access to a dataset blob.
    /// </summary>
    string GenerateSasUrl(string containerName, string blobName, TimeSpan expiry);
}
