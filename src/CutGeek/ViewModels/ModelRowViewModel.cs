using System.Windows.Input;
using Avalonia.Threading;
using CutGeek.Core.Models;
using CutGeek.Core.Services;

namespace CutGeek.ViewModels;

/// <summary>
/// One row on the Models screen: what a model is for, how big it is, and whether it is here yet.
///
/// Downloading is the only thing CutGeek ever fetches from the internet, and it only happens
/// when somebody presses the button on this row.
/// </summary>
public sealed class ModelRowViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private CancellationTokenSource? _cts;

    public ModelRowViewModel(CutoutModel model, ShellViewModel shell)
    {
        Model = model;
        _shell = shell;

        Download = new RelayCommand(() => _ = DownloadAsync());
        Cancel = new RelayCommand(() => _cts?.Cancel());
        Remove = new RelayCommand(RemoveModel);
    }

    public CutoutModel Model { get; }

    public string Name => Model.Name;
    public string Blurb => Model.Blurb;
    public string SizeText => IsDownloaded ? Bytes(Model.ApproxBytes) : Bytes(Model.ApproxBytes) + " download";

    /// <summary>Shown small under the row, because a pinned hash is only reassuring if you can see it.</summary>
    public string HashText => "SHA-256 " + Model.Sha256[..16] + "…";

    public bool IsDownloaded => ModelCatalog.IsDownloaded(Model);
    public bool IsMissing => !IsDownloaded && !IsDownloading;

    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (!SetField(ref _isDownloading, value)) return;
            OnPropertyChanged(nameof(IsMissing));
            OnPropertyChanged(nameof(CanRemove));
        }
    }

    public bool CanRemove => IsDownloaded && !IsDownloading;

    private double _progress;
    public double Progress { get => _progress; private set => SetField(ref _progress, value); }

    private string _note = "";
    public string Note { get => _note; private set => SetField(ref _note, value); }

    public ICommand Download { get; }
    public ICommand Cancel { get; }
    public ICommand Remove { get; }

    private async Task DownloadAsync()
    {
        if (IsDownloading || IsDownloaded) return;

        IsDownloading = true;
        Progress = 0;
        Note = "Starting…";
        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<double>(p =>
            {
                Progress = p * 100;
                Note = p >= 1
                    ? "Checking the file against its hash…"
                    : $"{p:P0} of {Bytes(Model.ApproxBytes)}";
            });

            await ModelCatalog.DownloadAsync(Model, progress, _cts.Token);
            Note = "";
        }
        catch (OperationCanceledException)
        {
            Note = "Cancelled. Nothing was kept.";
        }
        catch (Exception ex)
        {
            Log.Write($"Model {Model.Id}: {ex}");
            Note = "That download did not finish: " + ex.Message;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsDownloading = false;
            Refresh();
            _shell.OnModelsChanged(this);
        }
    }

    private void RemoveModel()
    {
        try
        {
            ModelCatalog.Delete(Model);
            Note = "";
        }
        catch (Exception ex)
        {
            Note = "It could not be removed: " + ex.Message;
        }

        Refresh();
        _shell.OnModelsChanged(this);
    }

    public void Refresh()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Refresh);
            return;
        }

        OnPropertyChanged(nameof(IsDownloaded));
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(SizeText));
    }

    private static string Bytes(long b) => b switch
    {
        >= 1_000_000_000 => $"{b / 1_000_000_000d:0.0} GB",
        >= 100_000_000 => $"{b / 1_000_000d:0} MB",
        _ => $"{b / 1_000_000d:0.0} MB"
    };

    public override string ToString() => $"{Name} — {SizeText}";
}
