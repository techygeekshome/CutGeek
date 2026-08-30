using TechyGeeksHome.Common;

namespace CutGeek;

/// <summary>
/// Everything the shared About window and update check need to know about this app. One place,
/// so the wording here and the wording on the product page can be kept in step.
/// </summary>
internal static class AppFacts
{
    public static readonly AppInfo Info = new()
    {
        Name = "CutGeek",
        Tagline = "Takes the background out of a photograph, at full size",
        Description =
            "Drop a photograph in and CutGeek writes out a copy with the background removed - " +
            "transparent, a flat colour, or the original blurred behind the subject. It runs the " +
            "U^2-Net models locally through the ONNX runtime. No account, no server, no upload, " +
            "no credits, and the result is exactly the size of the photograph that went in.",
        GitHubOwner = "techygeekshome",
        GitHubRepo = "CutGeek",
        ProductUrl = "https://techygeekshome.info/cutgeek/",
        IconUri = "avares://CutGeek/Assets/cutgeek.png",
        LicenceLine = "Free to use, including at work. GPL-3.0. No paid tier, ever.",
        Credits = new[]
        {
            new Credit("U^2-Net", "Apache-2.0", "https://github.com/xuebinqin/U-2-Net"),
            new Credit("IS-Net (DIS)", "Apache-2.0", "https://github.com/xuebinqin/DIS"),
            new Credit("ONNX model builds", "from rembg", "https://github.com/danielgatis/rembg"),
            new Credit("ONNX Runtime", "MIT", "https://onnxruntime.ai"),
            new Credit("SkiaSharp", "MIT", "https://github.com/mono/SkiaSharp"),
            new Credit("Avalonia", "MIT", "https://avaloniaui.net")
        }
    };
}
