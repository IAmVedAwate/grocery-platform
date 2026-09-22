using Application.Common;
using Domain.Catalog;
using Shared.Exceptions;

namespace Application.Catalog;

/// <summary>
/// Product registration is a P0 acceptance-criterion for speed
/// (docs/PRD.md §7) — this service intentionally requires only sku, name,
/// price, and tax rate; everything else is optional.
/// </summary>
public sealed class ProductApplicationService(
    IProductRepository products,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IStorageService storage,
    IColorExtractionService colorExtraction)
{
    private static readonly IReadOnlyDictionary<string, string> AllowedImageTypes = new Dictionary<string, string>
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp"
    };
    private const long MaxImageBytes = 5 * 1024 * 1024;
    public async Task<Product> CreateAsync(CreateProductRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.Barcode) && await products.BarcodeExistsAsync(request.Barcode, ct))
            throw new ConflictAppException($"Barcode '{request.Barcode}' is already in use.");

        var product = new Product(
            tenantContext.StoreId,
            request.Sku,
            request.Name,
            request.Price,
            request.TaxRatePercent,
            request.Barcode,
            request.CategoryId,
            request.BrandId,
            request.UnitId,
            request.LowStockThreshold);

        await products.AddAsync(product, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return product;
    }

    public async Task<Product> GetAsync(Guid id, CancellationToken ct)
    {
        return await products.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Product), id);
    }

    public async Task<PagedResult<Product>> ListAsync(PageRequest page, string? search, bool? isActive, CancellationToken ct)
    {
        var (items, totalCount) = await products.ListAsync(page.Skip, page.PageSize, search, isActive, ct);
        return new PagedResult<Product>
        {
            Items = items,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<Product> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Product), id);

        if (!string.IsNullOrWhiteSpace(request.Barcode)
            && request.Barcode != product.Barcode
            && await products.BarcodeExistsAsync(request.Barcode, ct))
            throw new ConflictAppException($"Barcode '{request.Barcode}' is already in use.");

        product.UpdateDetails(
            request.Name, request.Price, request.TaxRatePercent, request.Barcode,
            request.CategoryId, request.BrandId, request.UnitId, request.LowStockThreshold);

        await unitOfWork.SaveChangesAsync(ct);
        return product;
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Product), id);
        product.Deactivate();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SetImageAsync(Guid id, Stream content, string contentType, long contentLength, CancellationToken ct)
    {
        if (!AllowedImageTypes.TryGetValue(contentType, out var extension))
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["image"] = [$"Unsupported image type '{contentType}'. Allowed: {string.Join(", ", AllowedImageTypes.Keys)}."]
            });
        if (contentLength > MaxImageBytes)
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["image"] = [$"Image exceeds the {MaxImageBytes / 1024 / 1024}MB limit."]
            });

        var product = await products.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Product), id);
        var previousKey = product.ImageStorageKey;

        // Buffered once because both storage and color extraction need to
        // read the stream from the start, and a stream can only be
        // consumed once — SaveAsync copies it to disk, extraction decodes
        // it as an image, neither can go first against the original.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        buffer.Position = 0;
        var newKey = await storage.SaveAsync(buffer, extension, ct);

        buffer.Position = 0;
        var dominantColor = await colorExtraction.ExtractDominantColorAsync(buffer, ct);

        product.SetImage(newKey, dominantColor?.Hex);
        await unitOfWork.SaveChangesAsync(ct);

        // Best-effort cleanup, after the new key is safely committed —
        // an orphaned old file is a disk-space leak, not a correctness bug;
        // leaving a product with no image at all (if this ran first and
        // failed) would be.
        if (previousKey is not null)
            await storage.DeleteAsync(previousKey, ct);
    }

    public async Task<(Stream Content, string ContentType)> GetImageAsync(Guid id, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Product), id);
        if (product.ImageStorageKey is null)
            throw new NotFoundException("ProductImage", id);

        return await storage.OpenAsync(product.ImageStorageKey, ct)
            ?? throw new NotFoundException("ProductImage", id);
    }

    public async Task RemoveImageAsync(Guid id, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Product), id);
        var previousKey = product.ImageStorageKey;
        if (previousKey is null)
            return;

        product.ClearImage();
        await unitOfWork.SaveChangesAsync(ct);
        await storage.DeleteAsync(previousKey, ct);
    }
}

public sealed record CreateProductRequest(
    string Sku, string Name, decimal Price, decimal TaxRatePercent,
    string? Barcode, Guid? CategoryId, Guid? BrandId, Guid? UnitId, int LowStockThreshold);

public sealed record UpdateProductRequest(
    string Name, decimal Price, decimal TaxRatePercent,
    string? Barcode, Guid? CategoryId, Guid? BrandId, Guid? UnitId, int LowStockThreshold);
