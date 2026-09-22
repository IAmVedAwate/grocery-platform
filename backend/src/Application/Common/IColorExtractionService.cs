namespace Application.Common;

/// <summary>
/// Identifies a product's dominant color from an uploaded image (a
/// pretrained ONNX model, ported from the standalone ProductColorExtractor
/// practice project — see Infrastructure/ColorExtraction). Used to drive
/// the "find a product by color" browsing UX: humans often recall a
/// product's color before its exact name.
/// </summary>
public interface IColorExtractionService
{
    /// <returns>null if the image couldn't be decoded/processed — a
    /// missing color is a degraded experience, never a reason to fail
    /// the whole image upload.</returns>
    Task<DominantColor?> ExtractDominantColorAsync(Stream imageStream, CancellationToken ct);
}

public sealed record DominantColor(byte R, byte G, byte B)
{
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";
}
