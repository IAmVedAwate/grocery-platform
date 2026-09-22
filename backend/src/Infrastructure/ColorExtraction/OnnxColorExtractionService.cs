using Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Infrastructure.ColorExtraction;

/// <summary>
/// Ported from the standalone ProductColorExtractor practice project —
/// same model, same tensor contract, adapted to this project's
/// conventions (return null on failure instead of throwing; the model
/// path is resolved from the running assembly's own directory since the
/// .onnx file ships as Content alongside the DLL, not from
/// STORAGE_LOCAL_PATH like user-uploaded files).
///
/// Model contract (dominant_color.onnx): input "image", float32 NCHW
/// tensor [1, 3, H, W] with raw 0-255 pixel values (not normalized) — the
/// model resizes to 128x128 internally regardless of H/W. Output
/// "dominant_rgb", a float32[3] of R, G, B in 0-255.
///
/// Registered as a singleton (see Program.cs) so InferenceSession — which
/// loads and JITs the model graph — is created once at process startup,
/// not per request.
/// </summary>
public sealed class OnnxColorExtractionService : IColorExtractionService, IDisposable
{
    // A performance cap on the input fed to inference, not a requirement
    // of the model itself (see the class remarks above).
    private const int MaxLongestSide = 512;

    private readonly InferenceSession _session;
    private readonly ILogger<OnnxColorExtractionService> _logger;

    public OnnxColorExtractionService(ILogger<OnnxColorExtractionService> logger)
    {
        _logger = logger;
        var modelPath = Path.Combine(AppContext.BaseDirectory, "ColorExtraction", "Models", "dominant_color.onnx");
        _session = new InferenceSession(modelPath);
    }

    public async Task<DominantColor?> ExtractDominantColorAsync(Stream imageStream, CancellationToken ct)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgb24>(imageStream, ct);

            if (image.Width > MaxLongestSide || image.Height > MaxLongestSide)
            {
                var scale = (double)MaxLongestSide / Math.Max(image.Width, image.Height);
                var newWidth = Math.Max(1, (int)(image.Width * scale));
                var newHeight = Math.Max(1, (int)(image.Height * scale));
                image.Mutate(ctx => ctx.Resize(newWidth, newHeight));
            }

            var width = image.Width;
            var height = image.Height;

            // Pixels copied out to a plain array first — Span<T> is a ref
            // struct and can't be captured inside ProcessPixelRows' lambda,
            // so pixel access and tensor filling happen as ordinary code.
            var pixels = new Rgb24[width * height];
            image.CopyPixelDataTo(pixels.AsSpan());

            // NCHW layout, row-major: channel c, row y, col x lives at
            // c*H*W + y*W + x.
            var tensor = new DenseTensor<float>([1, 3, height, width]);
            var span = tensor.Buffer.Span;
            var channelStride = height * width;

            for (var i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                span[i] = pixel.R;
                span[channelStride + i] = pixel.G;
                span[(2 * channelStride) + i] = pixel.B;
            }

            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("image", tensor) };

            using var results = _session.Run(inputs);
            var output = results.First(v => v.Name == "dominant_rgb").AsEnumerable<float>().ToArray();

            var r = (byte)Math.Clamp(Math.Round(output[0]), 0, 255);
            var g = (byte)Math.Clamp(Math.Round(output[1]), 0, 255);
            var b = (byte)Math.Clamp(Math.Round(output[2]), 0, 255);

            return new DominantColor(r, g, b);
        }
        catch (Exception ex)
        {
            // Color is an enhancement on top of the image upload, not a
            // reason to fail it — an unrecognized/corrupt image still
            // saves fine, it just has no color badge.
            _logger.LogWarning(ex, "Dominant color extraction failed; product image will have no color.");
            return null;
        }
    }

    public void Dispose() => _session.Dispose();
}
