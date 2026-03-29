using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Errors;
using InfrastructureService.Configuration.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ports.OutBoundPorts.Storage;

namespace InfrastructureService.OutBoundAdapters.Storage;

public sealed class LocalFileStoragePort : IFileStoragePort
{
    private readonly string _rootPath;
    private readonly ILogger<LocalFileStoragePort> _logger;

    public LocalFileStoragePort(
        IOptions<StorageOptions> options,
        ILogger<LocalFileStoragePort> logger)
    {
        _logger = logger;
        var configuredPath = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = "storage";
        }

        _rootPath = Path.GetFullPath(configuredPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> UploadAsync(
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        var safeFileName = string.IsNullOrWhiteSpace(fileName)
            ? "blob.bin"
            : Path.GetFileName(fileName);

        var relativeKey = Path.Combine(
            DateTime.UtcNow.ToString("yyyy"),
            DateTime.UtcNow.ToString("MM"),
            DateTime.UtcNow.ToString("dd"),
            $"{Guid.NewGuid():N}_{safeFileName}");

        var fullPath = GetSafeFullPath(relativeKey);
        var parentDirectory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(output, cancellationToken);

        _logger.LogInformation("Stored file at key {FileKey} (content type {ContentType}).", relativeKey, contentType);
        return relativeKey.Replace('\\', '/');
    }

    public Task<Stream> DownloadAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(fileKey))
        {
            throw new ArgumentException("File key cannot be empty.", nameof(fileKey));
        }

        var fullPath = GetSafeFullPath(fileKey.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            throw new InfrastructureException("STORAGE_FILE_NOT_FOUND", $"File '{fileKey}' was not found.");
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    private string GetSafeFullPath(string relativeKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativeKey));
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, _rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InfrastructureException("STORAGE_INVALID_KEY", "Invalid file key path.");
        }

        return fullPath;
    }
}
