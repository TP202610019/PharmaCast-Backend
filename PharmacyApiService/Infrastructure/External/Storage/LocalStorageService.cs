using PharmacyApiService.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace PharmacyApiService.Infrastructure.External.Storage;

/// <summary>
/// Stores uploaded datasets on the local disk for development and presentations.
/// It preserves the same container/blob contract used by Azure so the application
/// services do not need to know which storage provider is active.
/// </summary>
public sealed class LocalStorageService : IBlobStorageService
{
    private readonly string _rootPath;

    public LocalStorageService(IHostEnvironment environment, IOptions<LocalStorageSettings> options)
    {
        var configuredRoot = options.Value.RootPath;
        _rootPath = Path.GetFullPath(
            Path.IsPathRooted(configuredRoot)
                ? configuredRoot
                : Path.Combine(environment.ContentRootPath, configuredRoot));
    }

    public async Task<string> UploadAsync(
        Stream stream,
        string containerName,
        string blobName,
        string contentType,
        CancellationToken ct = default)
    {
        _ = contentType;
        var path = ResolvePath(containerName, blobName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var destination = new FileStream(
            path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await stream.CopyToAsync(destination, ct);
        return path;
    }

    public Task<Stream?> DownloadAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = ResolvePath(containerName, blobName);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = ResolvePath(containerName, blobName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(ResolvePath(containerName, blobName)));
    }

    public Task MoveAsync(
        string sourceContainer,
        string sourceBlobName,
        string destinationContainer,
        string destinationBlobName,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var source = ResolvePath(sourceContainer, sourceBlobName);
        var destination = ResolvePath(destinationContainer, destinationBlobName);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(source, destination, overwrite: true);
        return Task.CompletedTask;
    }

    public string GetBlobUrl(string containerName, string blobName) => ResolvePath(containerName, blobName);

    public string GenerateSasUrl(string containerName, string blobName, TimeSpan expiry)
    {
        _ = expiry;
        return ResolvePath(containerName, blobName);
    }

    private string ResolvePath(string containerName, string blobName)
    {
        if (string.IsNullOrWhiteSpace(containerName) || string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException("Storage container and file path are required.");

        var containerRoot = Path.GetFullPath(Path.Combine(_rootPath, containerName));
        var candidate = Path.GetFullPath(Path.Combine(containerRoot, blobName));
        var prefix = containerRoot.EndsWith(Path.DirectorySeparatorChar)
            ? containerRoot
            : containerRoot + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Storage path must stay inside its container.");

        return candidate;
    }
}
