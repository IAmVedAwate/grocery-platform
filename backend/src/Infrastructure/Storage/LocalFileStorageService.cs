using Application.Common;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Storage;

/// <summary>
/// Dev/local-shape implementation of IStorageService — files live on disk
/// under STORAGE_LOCAL_PATH. The storage key is just the generated file
/// name (a GUID + extension); content type is derived from the extension
/// on read rather than stored separately, since this implementation never
/// serves anything but the handful of image types product images allow
/// (see ProductApplicationService.SetImageAsync).
/// </summary>
public sealed class LocalFileStorageService(IConfiguration configuration) : IStorageService
{
    private string RootPath => configuration["STORAGE_LOCAL_PATH"] ?? "./.data/documents";

    public async Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken ct)
    {
        Directory.CreateDirectory(RootPath);
        var key = $"{Guid.NewGuid():N}{fileExtension}";
        var path = Path.Combine(RootPath, key);

        await using var fileStream = File.Create(path);
        await content.CopyToAsync(fileStream, ct);

        return key;
    }

    public Task<(Stream Content, string ContentType)?> OpenAsync(string storageKey, CancellationToken ct)
    {
        var path = Path.Combine(RootPath, storageKey);
        if (!File.Exists(path))
            return Task.FromResult<(Stream, string)?>(null);

        Stream stream = File.OpenRead(path);
        return Task.FromResult<(Stream, string)?>((stream, ContentTypeFor(storageKey)));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = Path.Combine(RootPath, storageKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private static string ContentTypeFor(string storageKey) => Path.GetExtension(storageKey).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
