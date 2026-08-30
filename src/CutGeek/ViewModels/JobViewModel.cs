using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CutGeek.Core.Models;

namespace CutGeek.ViewModels;

/// <summary>
/// One image in the queue. Wraps a <see cref="CutoutJob"/> rather than replacing it, so the Core
/// project stays free of anything to do with the screen.
/// </summary>
public sealed class JobViewModel : ObservableObject, IDisposable
{
    public JobViewModel(CutoutJob job)
    {
        Job = job;
        OpenResult = new RelayCommand(() => OpenPath(Job.OutputPath));
        OpenFolder = new RelayCommand(() => OpenPath(
            Path.GetDirectoryName(Job.OutputPath ?? Job.SourcePath)));
    }

    public CutoutJob Job { get; }

    public string FileName => Job.FileName;

    public JobState State => Job.State;
    public string Status => Job.Status;

    public bool HasOutput => Job.State == JobState.Done && Job.OutputPath is not null;

    public string SizeText => Job.Width > 0 ? $"{Job.Width} × {Job.Height}" : "";

    public ICommand OpenResult { get; }
    public ICommand OpenFolder { get; }

    /// <summary>
    /// The finished image, ready for the preview. Held as a decoded bitmap rather than a path
    /// because the file on disk may be a JPEG on a colour backdrop while the preview always
    /// wants the transparency-aware PNG.
    /// </summary>
    private Bitmap? _preview;
    public Bitmap? Preview { get => _preview; private set => SetField(ref _preview, value); }

    public bool HasPreview => Preview is not null;

    public void SetPreview(byte[] pngBytes)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => SetPreview(pngBytes));
            return;
        }

        _preview?.Dispose();
        using var stream = new MemoryStream(pngBytes);
        Preview = new Bitmap(stream);
        OnPropertyChanged(nameof(HasPreview));
    }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; private set => SetField(ref _isSelected, value); }

    public void SetSelected(bool selected) => IsSelected = selected;

    /// <summary>
    /// The one coloured thing on the row. The same four meanings the rest of the range uses, so
    /// a glance reads the same everywhere.
    /// </summary>
    public IBrush StateBrush => Job.State switch
    {
        JobState.Done => Brush.Parse("#3BA55C"),
        JobState.Failed => Brush.Parse("#FF5E5B"),
        JobState.Running => Brush.Parse("#2E78D8"),
        JobState.Cancelled => Brush.Parse("#E0A62B"),
        _ => Brush.Parse("#4A5468")
    };

    public string StateText => Job.State switch
    {
        JobState.Done => "Done",
        JobState.Failed => "Failed",
        JobState.Running => "Working",
        JobState.Cancelled => "Stopped",
        _ => "Waiting"
    };

    /// <summary>
    /// Moves the job on and says why, in one call, so a state and its explanation can never
    /// disagree on screen.
    /// </summary>
    public void SetState(JobState state, string status)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => SetState(state, status));
            return;
        }

        Job.State = state;
        Job.Status = status;
        if (state == JobState.Failed) Job.Error = status;
        Refresh();
    }

    public void Refresh()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Refresh);
            return;
        }

        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(HasOutput));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(StateBrush));
        OnPropertyChanged(nameof(StateText));
    }

    private static void OpenPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No association, or the file has been moved since. Not worth a dialog.
        }
    }

    public void Dispose()
    {
        _preview?.Dispose();
        _preview = null;
    }
}
