namespace CutGeek.Core.Models;

/// <summary>
/// A segmentation model CutGeek can use.
/// </summary>
/// <param name="Id">File name stem, and the identifier used on disk.</param>
/// <param name="Name">What we call it on screen.</param>
/// <param name="ApproxBytes">Exact download size in bytes.</param>
/// <param name="Sha256">
/// The hash of the file we tested against. A download that does not match it is deleted rather
/// than used - see <c>ModelCatalog</c> for why that matters more here than it looks.
/// </param>
/// <param name="Url">Where it comes from.</param>
/// <param name="InputSize">Square input the network expects.</param>
/// <param name="Mean">Per-channel mean subtracted during normalisation, in RGB order.</param>
/// <param name="Std">Per-channel standard deviation, in RGB order.</param>
/// <param name="Blurb">One line: when to pick this one.</param>
public sealed record CutoutModel(
    string Id,
    string Name,
    long ApproxBytes,
    string Sha256,
    string Url,
    int InputSize,
    float[] Mean,
    float[] Std,
    string Blurb)
{
    public string FileName => $"{Id}.onnx";
}

/// <summary>What goes behind the subject once the background has been taken out.</summary>
public enum BackdropKind
{
    /// <summary>Nothing. A PNG with a real alpha channel.</summary>
    Transparent,

    /// <summary>A flat colour, for a passport photo or a product listing that wants white.</summary>
    Colour,

    /// <summary>The original background, blurred. Useful for a portrait that still needs context.</summary>
    Blur
}

/// <summary>One image waiting to be cut out, or one that has been.</summary>
public sealed class CutoutJob
{
    public required string SourcePath { get; init; }
    public string FileName => Path.GetFileName(SourcePath);

    public JobState State { get; set; } = JobState.Waiting;
    public string Status { get; set; } = "Waiting";

    /// <summary>Pixel size of the source, once it has been read.</summary>
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Where the cutout was written. Null until it has been.</summary>
    public string? OutputPath { get; set; }

    public string? Error { get; set; }
}

public enum JobState
{
    Waiting,
    Running,
    Done,
    Failed,
    Cancelled
}
