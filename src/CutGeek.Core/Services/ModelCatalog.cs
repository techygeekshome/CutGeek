using System.Security.Cryptography;
using CutGeek.Core.Models;

namespace CutGeek.Core.Services;

/// <summary>
/// The models CutGeek offers, where they come from and where they live on disk.
///
/// Nothing is shipped in the installer. The good models are 176 MB each and most people only
/// ever need one, so they are fetched on first use with the size shown before anything starts.
///
/// **Every download is checked against a pinned SHA-256 and deleted if it does not match.**
/// That is not ceremony. These files come from somebody else's GitHub release, they are run as
/// code by the ONNX runtime, and a release asset can be replaced by whoever owns the
/// repository. Pinning the hash means CutGeek runs the exact file that was tested or it runs
/// nothing at all.
/// </summary>
public static class ModelCatalog
{
    /// <summary>
    /// Under LocalApplicationData rather than beside the executable, so the portable build does
    /// not have to sit in a writable folder and a reinstall does not mean fetching 176 MB again.
    /// </summary>
    public static string ModelDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TechyGeeksHome", "CutGeek", "models");

    private const string Source = "https://github.com/danielgatis/rembg/releases/download/v0.0.0/";

    private static readonly float[] U2Mean = { 0.485f, 0.456f, 0.406f };
    private static readonly float[] U2Std = { 0.229f, 0.224f, 0.225f };
    private static readonly float[] Half = { 0.5f, 0.5f, 0.5f };
    private static readonly float[] One = { 1.0f, 1.0f, 1.0f };

    public static IReadOnlyList<CutoutModel> All { get; } = new[]
    {
        new CutoutModel("u2netp", "Quick", 4_574_861,
            "309c8469258dda742793dce0ebea8e6dd393174f89934733ecc8b14c76f4ddd8",
            Source + "u2netp.onnx", 320, U2Mean, U2Std,
            "A twentieth of the size and several times faster. Fine for a clear subject on a plain background."),

        new CutoutModel("u2net", "Standard", 175_997_641,
            "8d10d2f3bb75ae3b6d527c77944fc5e7dcd94b29809d47a739a7a728a912b491",
            Source + "u2net.onnx", 320, U2Mean, U2Std,
            "The usual choice. Handles most photographs well. Start here."),

        new CutoutModel("u2net_human_seg", "People", 175_997_641,
            "01eb6a29a5c4d8edb30b56adad9bb3a2a0535338e480724a213e0acfd2d1c73c",
            Source + "u2net_human_seg.onnx", 320, U2Mean, U2Std,
            "Trained on people. Noticeably better on portraits, and only on portraits."),

        new CutoutModel("isnet-general-use", "Detailed", 178_648_008,
            "60920e99c45464f2ba57bee2ad08c919a52bbf852739e96947fbb4358c0d964a",
            Source + "isnet-general-use.onnx", 1024, Half, One,
            "Reads the image at 1024 pixels instead of 320, so hair and thin edges survive. Slower."),
    };

    public static CutoutModel Default => All.Single(m => m.Id == "u2net");

    public static string PathFor(CutoutModel model) => Path.Combine(ModelDirectory, model.FileName);

    /// <summary>
    /// Whether a model is present and the right size. The size check catches the half-file left
    /// by a connection that dropped, which otherwise fails at load time with an error that
    /// means nothing to anybody.
    /// </summary>
    public static bool IsDownloaded(CutoutModel model)
    {
        var p = PathFor(model);
        return File.Exists(p) && new FileInfo(p).Length == model.ApproxBytes;
    }

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };

    /// <summary>
    /// Downloads a model to a .part file, hashes it, and only then puts it in place. A failed,
    /// cancelled or tampered download can therefore never leave something behind that looks
    /// like a working model.
    /// </summary>
    public static async Task DownloadAsync(
        CutoutModel model,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(ModelDirectory);
        var final = PathFor(model);
        var part = final + ".part";

        if (File.Exists(part)) File.Delete(part);

        try
        {
            using var response = await Http.GetAsync(model.Url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? model.ApproxBytes;

            await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var dest = File.Create(part))
            {
                var buffer = new byte[1 << 20];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await dest.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    done += read;
                    progress?.Report(Math.Min(1.0, (double)done / total));
                }
            }

            var actual = await Sha256Async(part, ct).ConfigureAwait(false);
            if (!string.Equals(actual, model.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(part);
                throw new InvalidDataException(
                    $"The downloaded file is not the one CutGeek was tested with, so it has been deleted. " +
                    $"Expected {model.Sha256[..12]}…, got {actual[..12]}….");
            }

            if (File.Exists(final)) File.Delete(final);
            File.Move(part, final);
            progress?.Report(1.0);
        }
        catch
        {
            if (File.Exists(part)) { try { File.Delete(part); } catch (IOException) { } }
            throw;
        }
    }

    public static async Task<string> Sha256Async(string path, CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Removes a downloaded model. Used by the Models screen.</summary>
    public static void Delete(CutoutModel model)
    {
        var p = PathFor(model);
        if (File.Exists(p)) File.Delete(p);
    }
}
