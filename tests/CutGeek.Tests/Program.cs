using CutGeek.Core.Models;
using CutGeek.Core.Services;
using SkiaSharp;

// A plain console program rather than a test framework, so `dotnet run` proves the build on any
// machine with nothing installed. Same shape as the checks in DriverGeek and CleanGeek.

var failures = 0;

void Check(string name, bool ok, string? detail = null)
{
    Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail is null ? "" : $"  ({detail})"));
    if (!ok) failures++;
}

void Skip(string name, string why) => Console.WriteLine($"SKIP  {name} ({why})");

var temp = Path.Combine(Path.GetTempPath(), "cutgeek-tests-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(temp);

try
{
    // ---------------------------------------------------------------- the catalogue

    Check("four models offered", ModelCatalog.All.Count == 4);
    Check("default is the standard one", ModelCatalog.Default.Id == "u2net");
    Check("every model has a pinned hash",
        ModelCatalog.All.All(m => m.Sha256.Length == 64));
    Check("every model has a size",
        ModelCatalog.All.All(m => m.ApproxBytes > 1_000_000));
    Check("model path is under LocalApplicationData",
        ModelCatalog.PathFor(ModelCatalog.Default)
            .StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));

    // ---------------------------------------------------------------- supported files

    Check("jpg is supported", ImageIO.IsSupported("a.JPG"));
    Check("png is supported", ImageIO.IsSupported("a.png"));
    Check("txt is not", !ImageIO.IsSupported("a.txt"));

    // ---------------------------------------------------------------- output naming

    var stub = Path.Combine(temp, "photo.jpg");
    File.WriteAllText(stub, "not really a jpeg");

    var first = ImageIO.NextFreePath(stub, temp, "-cutout", ".png");
    Check("output is named after the source",
        Path.GetFileName(first) == "photo-cutout.png", Path.GetFileName(first));

    File.WriteAllText(first, "x");
    var second = ImageIO.NextFreePath(stub, temp, "-cutout", ".png");
    Check("a second run does not overwrite the first",
        Path.GetFileName(second) == "photo-cutout (2).png", Path.GetFileName(second));
    Check("the first output is still there", File.ReadAllText(first) == "x");

    var elsewhere = Path.Combine(temp, "out");
    var third = ImageIO.NextFreePath(stub, elsewhere, "-cutout", ".png");
    Check("an output folder is honoured and created",
        Path.GetDirectoryName(third) == elsewhere && Directory.Exists(elsewhere));

    // ---------------------------------------------------------------- reading images

    var square = Path.Combine(temp, "square.png");
    using (var b = new SKBitmap(new SKImageInfo(40, 24, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
    {
        using (var c = new SKCanvas(b))
        {
            c.Clear(SKColors.White);
            c.DrawRect(8, 4, 24, 16, new SKPaint { Color = SKColors.Red });
        }
        ImageIO.Save(b, square, jpeg: false);
    }

    Check("a written png can be read back", File.Exists(square) && new FileInfo(square).Length > 0);

    using (var back = ImageIO.Load(square))
    {
        Check("it comes back the same size", back.Width == 40 && back.Height == 24,
            $"{back.Width}x{back.Height}");
        Check("it comes back as straight RGBA",
            back.ColorType == SKColorType.Rgba8888 && back.AlphaType == SKAlphaType.Unpremul);
        Check("the pixels survived", back.GetPixel(20, 12).Red == 255 && back.GetPixel(1, 1).Blue == 255);
    }

    var notAnImage = Path.Combine(temp, "nope.png");
    File.WriteAllText(notAnImage, "this is not a png");
    var threw = false;
    try { ImageIO.Load(notAnImage).Dispose(); } catch (ImageReadException) { threw = true; }
    Check("a file that is not an image is refused clearly", threw);

    // ---------------------------------------------------------------- the real thing

    var modelPath = Environment.GetEnvironmentVariable("CG_TEST_MODEL");
    var samplePath = Environment.GetEnvironmentVariable("CG_TEST_IMAGE");

    if (modelPath is null || samplePath is null || !File.Exists(modelPath) || !File.Exists(samplePath))
    {
        Skip("end-to-end cutout", "set CG_TEST_MODEL and CG_TEST_IMAGE to run it");
    }
    else
    {
        var id = Path.GetFileNameWithoutExtension(modelPath);
        var model = ModelCatalog.All.FirstOrDefault(m => m.Id == id) ?? ModelCatalog.Default;

        using var engine = new CutoutEngine();
        using var source = ImageIO.Load(samplePath);
        using var cut = engine.Run(samplePath, model, modelPath, BackdropKind.Transparent, SKColors.Transparent);

        Check("the cutout is the full size of the original",
            cut.Width == source.Width && cut.Height == source.Height,
            $"{cut.Width}x{cut.Height} vs {source.Width}x{source.Height}");

        long opaque = 0, clear = 0;
        for (var y = 0; y < cut.Height; y += 4)
        for (var x = 0; x < cut.Width; x += 4)
        {
            var a = cut.GetPixel(x, y).Alpha;
            if (a > 200) opaque++;
            else if (a < 32) clear++;
        }

        Check("something was kept", opaque > 0, opaque.ToString());
        Check("something was removed", clear > 0, clear.ToString());
        Check("it did not just keep everything", clear > opaque / 20,
            $"kept {opaque}, removed {clear}");

        var outPath = ImageIO.NextFreePath(samplePath, temp, "-cutout", ".png");
        ImageIO.Save(cut, outPath, jpeg: false);
        Check("the cutout writes a readable png", File.Exists(outPath) && new FileInfo(outPath).Length > 1000);

        using var white = engine.Run(samplePath, model, modelPath, BackdropKind.Colour, SKColors.White);
        var everyPixelOpaque = true;
        for (var y = 0; y < white.Height && everyPixelOpaque; y += 8)
        for (var x = 0; x < white.Width; x += 8)
            if (white.GetPixel(x, y).Alpha != 255) { everyPixelOpaque = false; break; }

        Check("a colour backdrop leaves no transparency", everyPixelOpaque);
    }
}
finally
{
    try { Directory.Delete(temp, true); } catch (IOException) { }
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures == 0 ? 0 : 1;
