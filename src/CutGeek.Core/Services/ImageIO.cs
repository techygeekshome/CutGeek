using SkiaSharp;

namespace CutGeek.Core.Services;

/// <summary>
/// Reading and writing image files. Kept apart from the segmentation so there is one place to
/// look when the question is "what did it open, and what did it save".
/// </summary>
public static class ImageIO
{
    public static readonly string[] SupportedExtensions =
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".heif", ".heic"
    };

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>
    /// Loads an image at its real size, upright, in straight (un-premultiplied) RGBA.
    ///
    /// The upright part matters more than it sounds. A photograph off a phone is very often
    /// stored sideways with an EXIF tag saying which way up it goes; decoders that ignore the
    /// tag produce a rotated cutout, which looks like the app is broken. Skia exposes the tag
    /// on the codec but does not apply it, so it is applied here.
    /// </summary>
    public static SKBitmap Load(string path)
    {
        using var codec = SKCodec.Create(path)
            ?? throw new ImageReadException($"{Path.GetFileName(path)} is not an image CutGeek can read.");

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height,
            SKColorType.Rgba8888, SKAlphaType.Unpremul);

        var decoded = new SKBitmap(info);
        var result = codec.GetPixels(info, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            decoded.Dispose();
            throw new ImageReadException($"{Path.GetFileName(path)} could not be decoded ({result}).");
        }

        return Orient(decoded, codec.EncodedOrigin);
    }

    /// <summary>Applies an EXIF orientation, returning the original bitmap when there is nothing to do.</summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.Default or SKEncodedOrigin.TopLeft) return source;

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        var w = swap ? source.Height : source.Width;
        var h = swap ? source.Width : source.Height;

        var rotated = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(rotated))
        {
            canvas.Clear(SKColors.Transparent);
            switch (origin)
            {
                case SKEncodedOrigin.TopRight:    canvas.Scale(-1, 1, w / 2f, 0); break;
                case SKEncodedOrigin.BottomRight: canvas.RotateDegrees(180, w / 2f, h / 2f); break;
                case SKEncodedOrigin.BottomLeft:  canvas.Scale(1, -1, 0, h / 2f); break;
                case SKEncodedOrigin.LeftTop:     canvas.RotateDegrees(90, 0, 0); canvas.Scale(1, -1, 0, 0); break;
                case SKEncodedOrigin.RightTop:    canvas.Translate(w, 0); canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.RightBottom: canvas.Translate(w, 0); canvas.RotateDegrees(90); canvas.Scale(1, -1, 0, source.Height / 2f); break;
                case SKEncodedOrigin.LeftBottom:  canvas.Translate(0, h); canvas.RotateDegrees(-90); break;
            }
            canvas.DrawBitmap(source, 0, 0);
        }

        source.Dispose();
        return rotated;
    }

    /// <summary>
    /// Saves a bitmap. PNG keeps the transparency; JPEG is offered only where there is none to
    /// keep, because a JPEG with a "transparent" background is just a black one.
    /// </summary>
    public static void Save(SKBitmap bitmap, string path, bool jpeg, int jpegQuality = 92)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png,
            jpeg ? jpegQuality : 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }

    /// <summary>
    /// The source file's own name with a suffix, in the chosen folder. Nothing is ever
    /// overwritten: if the name is taken, the next one is numbered. Quietly replacing somebody's
    /// earlier cutout because they ran the same photo twice is not acceptable behaviour.
    /// </summary>
    public static string NextFreePath(string sourcePath, string? outputDirectory, string suffix, string extension)
    {
        var dir = outputDirectory ?? Path.GetDirectoryName(sourcePath) ?? ".";
        Directory.CreateDirectory(dir);
        var stem = Path.GetFileNameWithoutExtension(sourcePath) + suffix;

        var candidate = Path.Combine(dir, stem + extension);
        if (!File.Exists(candidate)) return candidate;

        for (var n = 2; n < 1000; n++)
        {
            candidate = Path.Combine(dir, $"{stem} ({n}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }

        return Path.Combine(dir, $"{stem} {DateTime.Now:yyyyMMdd-HHmmss}{extension}");
    }
}

public sealed class ImageReadException : Exception
{
    public ImageReadException(string message) : base(message) { }
}
