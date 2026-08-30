using CutGeek.Core.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace CutGeek.Core.Services;

/// <summary>
/// Works out which pixels are the subject, and puts the chosen backdrop behind them.
///
/// Everything happens in this process on this machine. The image is never uploaded, and the
/// only file this class touches is the one it is asked to read. Writing the result out is
/// <see cref="ImageIO"/>'s job.
///
/// **The output is the full size of the input.** The network itself only ever sees a small
/// square - 320 or 1024 pixels - and produces a mask at that size, which is then scaled back up
/// and applied to the original pixels. That is the whole trick, and it is the reason CutGeek can
/// give away at full resolution what the web services charge for: the expensive part never
/// depended on the resolution in the first place.
/// </summary>
public sealed class CutoutEngine : IDisposable
{
    private InferenceSession? _session;
    private string? _loadedModelPath;

    private InferenceSession GetSession(string modelPath)
    {
        if (_session is not null && _loadedModelPath == modelPath) return _session;

        _session?.Dispose();

        var options = new SessionOptions
        {
            // One image at a time, all cores on it. Two images at once on a four-core machine
            // makes both slower and the progress meaningless.
            IntraOpNumThreads = Environment.ProcessorCount,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        _session = new InferenceSession(modelPath, options);
        _loadedModelPath = modelPath;
        return _session;
    }

    /// <summary>
    /// Produces the finished image. <paramref name="colour"/> is used only when
    /// <paramref name="backdrop"/> is <see cref="BackdropKind.Colour"/>.
    /// </summary>
    public SKBitmap Run(
        string sourcePath,
        CutoutModel model,
        string modelPath,
        BackdropKind backdrop,
        SKColor colour,
        CancellationToken ct = default)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                "That model has not been downloaded yet. Open Models and download one first.", modelPath);

        using var source = ImageIO.Load(sourcePath);
        ct.ThrowIfCancellationRequested();

        using var mask = ComputeMask(source, model, modelPath, ct);
        ct.ThrowIfCancellationRequested();

        return Compose(source, mask, backdrop, colour);
    }

    /// <summary>
    /// Runs the network and returns a grey mask the same size as the source: 255 is subject,
    /// 0 is background, and everything between is the soft edge that makes hair look right.
    /// </summary>
    private SKBitmap ComputeMask(SKBitmap source, CutoutModel model, string modelPath, CancellationToken ct)
    {
        var n = model.InputSize;

        using var small = new SKBitmap(new SKImageInfo(n, n, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(small))
        using (var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true })
        {
            canvas.Clear(SKColors.Black);
            canvas.DrawBitmap(source, new SKRect(0, 0, source.Width, source.Height),
                new SKRect(0, 0, n, n), paint);
        }

        var pixels = small.GetPixelSpan();
        var input = new DenseTensor<float>(new[] { 1, 3, n, n });

        // The reference implementation divides by the image's own maximum channel value before
        // normalising, so a dark photograph is stretched rather than left dim. Reproduced here
        // deliberately - dropping it changes the mask on low-contrast images.
        byte max = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i] > max) max = pixels[i];
            if (pixels[i + 1] > max) max = pixels[i + 1];
            if (pixels[i + 2] > max) max = pixels[i + 2];
        }
        if (max == 0) max = 1;

        var plane = n * n;
        for (var p = 0; p < plane; p++)
        {
            var o = p * 4;
            for (var c = 0; c < 3; c++)
                input[0, c, p / n, p % n] = (pixels[o + c] / (float)max - model.Mean[c]) / model.Std[c];
        }

        ct.ThrowIfCancellationRequested();

        var session = GetSession(modelPath);
        var inputName = session.InputMetadata.Keys.First();

        using var results = session.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, input) });
        var output = results.First().AsTensor<float>();

        // The network's first output is the finest of its several side outputs. Its values are
        // not bounded, so they are stretched to 0..1 over the image before being used.
        var flat = new float[plane];
        var lo = float.MaxValue;
        var hi = float.MinValue;
        for (var p = 0; p < plane; p++)
        {
            var v = output[0, 0, p / n, p % n];
            flat[p] = v;
            if (v < lo) lo = v;
            if (v > hi) hi = v;
        }

        var range = hi - lo;
        if (range <= float.Epsilon) range = 1;

        using var maskSmall = new SKBitmap(new SKImageInfo(n, n, SKColorType.Gray8, SKAlphaType.Opaque));
        unsafe
        {
            var dst = (byte*)maskSmall.GetPixels().ToPointer();
            for (var p = 0; p < plane; p++)
                dst[p] = (byte)Math.Clamp((flat[p] - lo) / range * 255f + 0.5f, 0, 255);
        }

        ct.ThrowIfCancellationRequested();

        // Back up to the real size. A bilinear enlargement is what softens the mask edge into
        // something that does not look cut out with scissors.
        return maskSmall.Resize(new SKImageInfo(source.Width, source.Height,
            SKColorType.Gray8, SKAlphaType.Opaque), SKFilterQuality.High);
    }

    /// <summary>Applies the mask as the alpha channel, then puts the chosen backdrop behind it.</summary>
    private static SKBitmap Compose(SKBitmap source, SKBitmap mask, BackdropKind backdrop, SKColor colour)
    {
        var info = new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var subject = new SKBitmap(info);

        unsafe
        {
            var src = (byte*)source.GetPixels().ToPointer();
            var msk = (byte*)mask.GetPixels().ToPointer();
            var dst = (byte*)subject.GetPixels().ToPointer();

            var srcStride = source.RowBytes;
            var mskStride = mask.RowBytes;
            var dstStride = subject.RowBytes;

            for (var y = 0; y < source.Height; y++)
            {
                var sRow = src + (long)y * srcStride;
                var mRow = msk + (long)y * mskStride;
                var dRow = dst + (long)y * dstStride;

                for (var x = 0; x < source.Width; x++)
                {
                    var so = x * 4;
                    dRow[so] = sRow[so];
                    dRow[so + 1] = sRow[so + 1];
                    dRow[so + 2] = sRow[so + 2];
                    // The source's own alpha is respected: a PNG that was already partly
                    // transparent does not become opaque just because the subject mask says so.
                    dRow[so + 3] = (byte)(mRow[x] * sRow[so + 3] / 255);
                }
            }
        }

        if (backdrop == BackdropKind.Transparent) return subject;

        var result = new SKBitmap(info);
        using (var canvas = new SKCanvas(result))
        {
            canvas.Clear(SKColors.Transparent);

            if (backdrop == BackdropKind.Colour)
            {
                canvas.Clear(colour);
            }
            else
            {
                // Blur radius scaled to the image, so a phone photo and a 24-megapixel one look
                // the same rather than one of them being barely touched.
                var sigma = Math.Max(source.Width, source.Height) / 60f;
                using var paint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(sigma, sigma) };
                canvas.DrawBitmap(source, 0, 0, paint);
            }

            canvas.DrawBitmap(subject, 0, 0);
        }

        subject.Dispose();
        return result;
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}
