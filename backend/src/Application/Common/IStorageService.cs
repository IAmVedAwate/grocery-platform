namespace Application.Common;

/// <summary>
/// Binary file storage, abstracted so the local-filesystem implementation
/// (Infrastructure/Storage/LocalFileStorageService) can be swapped for
/// Azure Blob Storage later (docs/PRD.md §26) without any Application/Api
/// code change — only Program.cs's DI registration would need to branch
/// on STORAGE_PROVIDER.
/// </summary>
public interface IStorageService
{
    /// <returns>An opaque storage key — callers persist this, not a path/URL.</returns>
    Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken ct);

    /// <returns>The file's bytes and content type, or null if the key doesn't exist.</returns>
    Task<(Stream Content, string ContentType)?> OpenAsync(string storageKey, CancellationToken ct);

    Task DeleteAsync(string storageKey, CancellationToken ct);
}
