using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PharmacyApiService.Application.Interfaces;
using PharmacyApiService.Domain.Exceptions;

namespace PharmacyApiService.Infrastructure.External.Storage;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _client;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly BlobStorageSettings _settings;

    public BlobStorageService(IOptions<BlobStorageSettings> options, ILogger<BlobStorageService> logger)
    {
        _logger = logger;
        _settings = options.Value;

        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
            throw new InvalidOperationException("AzureBlob:ConnectionString is not configured.");

        _client = new BlobServiceClient(_settings.ConnectionString);
    }

    public async Task<string> UploadAsync(Stream stream, string containerName, string blobName, string contentType, CancellationToken ct = default)
    {
        try
        {
            var container = _client.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            var blob = container.GetBlobClient(blobName);
            var uploadOptions = new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } };
            await blob.UploadAsync(stream, uploadOptions, ct);
            return blob.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload blob {Container}/{Blob}.", containerName, blobName);
            throw new ExternalServiceException("Azure Blob Storage", "The file could not be uploaded.");
        }
    }

    public async Task<Stream?> DownloadAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        try
        {
            var blob = _client.GetBlobContainerClient(containerName).GetBlobClient(blobName);
            if (!await blob.ExistsAsync(ct)) return null;
            var result = await blob.DownloadStreamingAsync(cancellationToken: ct);
            return result.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download blob {Container}/{Blob}.", containerName, blobName);
            throw new ExternalServiceException("Azure Blob Storage", "The file could not be downloaded.");
        }
    }

    public async Task DeleteAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        try
        {
            var blob = _client.GetBlobContainerClient(containerName).GetBlobClient(blobName);
            await blob.DeleteIfExistsAsync(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete blob {Container}/{Blob}.", containerName, blobName);
            throw new ExternalServiceException("Azure Blob Storage", "The file could not be deleted.");
        }
    }

    public async Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        try
        {
            var blob = _client.GetBlobContainerClient(containerName).GetBlobClient(blobName);
            return await blob.ExistsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence of blob {Container}/{Blob}.", containerName, blobName);
            throw new ExternalServiceException("Azure Blob Storage", "The file existence check failed.");
        }
    }

    public async Task MoveAsync(string sourceContainer, string sourceBlobName, string destinationContainer, string destinationBlobName, CancellationToken ct = default)
    {
        try
        {
            var srcBlob = _client.GetBlobContainerClient(sourceContainer).GetBlobClient(sourceBlobName);
            var dstContainer = _client.GetBlobContainerClient(destinationContainer);
            await dstContainer.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            var dstBlob = dstContainer.GetBlobClient(destinationBlobName);
            await dstBlob.StartCopyFromUriAsync(srcBlob.Uri, cancellationToken: ct);
            await srcBlob.DeleteIfExistsAsync(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move blob {SrcContainer}/{Src} to {DstContainer}/{Dst}.", sourceContainer, sourceBlobName, destinationContainer, destinationBlobName);
            throw new ExternalServiceException("Azure Blob Storage", "The file could not be moved.");
        }
    }

    public string GetBlobUrl(string containerName, string blobName)
        => $"{_settings.BaseUrl}/{containerName}/{blobName}";

    public string GenerateSasUrl(string containerName, string blobName, TimeSpan expiry)
    {
        var blob = _client.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        var sasUri = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(expiry));
        return sasUri.ToString();
    }
}
