using Application.Catalog;
using Application.Common;
using Domain.Catalog;
using Moq;
using Shared.Exceptions;

namespace Unit.Catalog;

/// <summary>
/// Every other test suite in this project hits real infrastructure
/// (real SQL Server, real ONNX model, real file I/O) on purpose — see
/// docs/testing/testing-strategy.md. This file is the deliberate exception:
/// SetImageAsync's business-rule branches (reject before touching storage,
/// degrade gracefully when color extraction fails, delete the old file
/// only after the new one is committed) are about call ordering and
/// decisions, not about proving the database/model/filesystem itself
/// works — that's exactly the shape a mock-based unit test is for, and
/// exactly what was missing from this project (docs/checkpoints/skills-inventory.md).
/// </summary>
public class ProductApplicationServiceTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IStorageService> _storage = new();
    private readonly Mock<IColorExtractionService> _colorExtraction = new();
    private readonly Mock<IAuditWriter> _auditWriter = new();

    private ProductApplicationService CreateSut() => new(
        _products.Object, _tenantContext.Object, _unitOfWork.Object,
        _storage.Object, _colorExtraction.Object, _auditWriter.Object);

    private static Product NewProduct() => new(Guid.NewGuid(), "SKU-1", "Test Product", 10m, 0m);

    [Fact]
    public async Task SetImageAsync_RejectsUnsupportedContentType_BeforeTouchingStorageOrTheModel()
    {
        var sut = CreateSut();
        var product = NewProduct();
        _products.Setup(p => p.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            sut.SetImageAsync(product.Id, Stream.Null, "application/pdf", contentLength: 100, CancellationToken.None));

        // The whole point of validating content type first: a bad upload
        // never reaches storage or the model at all.
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _colorExtraction.Verify(c => c.ExtractDominantColorAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetImageAsync_RejectsOversizedImage_BeforeTouchingStorage()
    {
        var sut = CreateSut();
        var product = NewProduct();
        _products.Setup(p => p.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            sut.SetImageAsync(product.Id, Stream.Null, "image/png", contentLength: 6 * 1024 * 1024, CancellationToken.None));

        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetImageAsync_WhenColorExtractionReturnsNull_StillSavesTheImage_WithNoColor()
    {
        var sut = CreateSut();
        var product = NewProduct();
        _products.Setup(p => p.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _storage.Setup(s => s.SaveAsync(It.IsAny<Stream>(), ".png", It.IsAny<CancellationToken>())).ReturnsAsync("new-key.png");
        // An unrecognizable image degrades gracefully — this is exactly
        // the contract OnnxColorExtractionService promises on a decode
        // failure (Infrastructure/ColorExtraction/OnnxColorExtractionService.cs).
        _colorExtraction.Setup(c => c.ExtractDominantColorAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync((DominantColor?)null);

        await sut.SetImageAsync(product.Id, new MemoryStream([1, 2, 3]), "image/png", contentLength: 3, CancellationToken.None);

        Assert.Equal("new-key.png", product.ImageStorageKey);
        Assert.Null(product.DominantColorHex);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetImageAsync_ReplacingAnExistingImage_DeletesTheOldFile_OnlyAfterTheNewOneIsSaved()
    {
        var sut = CreateSut();
        var product = NewProduct();
        product.SetImage("old-key.png", "#111111");
        _products.Setup(p => p.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _colorExtraction.Setup(c => c.ExtractDominantColorAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DominantColor(10, 20, 30));

        var callOrder = new List<string>();
        _storage.Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save"))
            .ReturnsAsync("new-key.png");
        _storage.Setup(s => s.DeleteAsync("old-key.png", It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("delete"))
            .Returns(Task.CompletedTask);

        await sut.SetImageAsync(product.Id, new MemoryStream([1, 2, 3]), "image/png", contentLength: 3, CancellationToken.None);

        Assert.Equal(["save", "delete"], callOrder);
        Assert.Equal("new-key.png", product.ImageStorageKey);
        Assert.Equal("#0A141E", product.DominantColorHex);
    }

    [Fact]
    public async Task SetImageAsync_ProductNotFound_ThrowsWithoutTouchingStorage()
    {
        var sut = CreateSut();
        var missingId = Guid.NewGuid();
        _products.Setup(p => p.GetByIdAsync(missingId, It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.SetImageAsync(missingId, Stream.Null, "image/png", contentLength: 10, CancellationToken.None));

        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
