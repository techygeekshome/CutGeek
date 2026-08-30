using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CutGeek.Core.Models;
using CutGeek.Core.Services;
using SkiaSharp;

namespace CutGeek.ViewModels;

/// <summary>
/// The whole application's state. CutGeek is small enough that one view model is honest -
/// splitting it would be ceremony rather than structure.
/// </summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly CutoutEngine _engine = new();
    private CancellationTokenSource? _cts;

    public ShellViewModel()
    {
        ShowCutOut = new RelayCommand(() => Page = "CutOut");
        ShowModels = new RelayCommand(() => Page = "Models");
        ShowSettings = new RelayCommand(() => Page = "Settings");

        foreach (var m in ModelCatalog.All)
            Models.Add(new ModelRowViewModel(m, this));

        SelectedModel = Models.FirstOrDefault(m => m.IsDownloaded)
                        ?? Models.First(m => m.Model.Id == ModelCatalog.Default.Id);

        Jobs.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasJobs));
            OnPropertyChanged(nameof(StatusLine));
            OnPropertyChanged(nameof(CanStart));
        };

        RefreshReadiness();
    }

    // ---------------------------------------------------------------- navigation

    private string _page = "CutOut";
    public string Page
    {
        get => _page;
        set
        {
            if (!SetField(ref _page, value)) return;
            OnPropertyChanged(nameof(IsCutOut));
            OnPropertyChanged(nameof(IsModels));
            OnPropertyChanged(nameof(IsSettings));
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(StatusLine));
        }
    }

    public bool IsCutOut => Page == "CutOut";
    public bool IsModels => Page == "Models";
    public bool IsSettings => Page == "Settings";

    public ICommand ShowCutOut { get; }
    public ICommand ShowModels { get; }
    public ICommand ShowSettings { get; }

    // ---------------------------------------------------------------- chrome

    public string BrandName => "CutGeek";
    public string BrandBy => "by TechyGeeksHome";
    public string VersionText => TechyGeeksHome.Common.AppInfo.CurrentVersionText;

    public string ModelFolder => ModelCatalog.ModelDirectory;

    public string PageTitle => Page switch
    {
        "Models" => "Models",
        "Settings" => "Settings",
        _ => "Cut out"
    };

    /// <summary>
    /// The one line under the title. Every app in the range says what was found and what was
    /// changed here, never a bare "Ready".
    /// </summary>
    public string StatusLine => Page switch
    {
        "Models" => $"{Models.Count(m => m.IsDownloaded)} of {Models.Count} downloaded · kept in {ModelCatalog.ModelDirectory}",
        "Settings" => "What CutGeek will and will not do, in plain words.",
        _ => Jobs.Count == 0
            ? "Drop photographs here. They are read on this machine, cut out at their full size, and nothing is uploaded."
            : $"{Jobs.Count} image{(Jobs.Count == 1 ? "" : "s")} · {Jobs.Count(j => j.State == JobState.Done)} done"
              + (Jobs.Any(j => j.State == JobState.Failed) ? $" · {Jobs.Count(j => j.State == JobState.Failed)} failed" : "")
    };

    // ---------------------------------------------------------------- readiness

    private string _readiness = "";
    public string Readiness { get => _readiness; private set => SetField(ref _readiness, value); }

    private bool _hasReadinessProblem;
    public bool HasReadinessProblem { get => _hasReadinessProblem; private set => SetField(ref _hasReadinessProblem, value); }

    public void RefreshReadiness()
    {
        foreach (var m in Models) m.Refresh();

        if (!Models.Any(m => m.IsDownloaded))
        {
            Readiness = "No model has been downloaded yet. Open Models and download one - " +
                        "Standard is the usual choice at about 176 MB. Nothing is downloaded without you asking.";
            HasReadinessProblem = true;
        }
        else
        {
            Readiness = "";
            HasReadinessProblem = false;
        }

        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(StatusLine));
    }

    /// <summary>
    /// Called by a model row after a download or a removal. If somebody has just fetched their
    /// first model, select it for them rather than making them find the dropdown.
    /// </summary>
    public void OnModelsChanged(ModelRowViewModel row)
    {
        if (row.IsDownloaded && SelectedModel is not { IsDownloaded: true })
            SelectedModel = row;

        RefreshReadiness();
    }

    // ---------------------------------------------------------------- the queue

    public ObservableCollection<JobViewModel> Jobs { get; } = new();

    public void AddFiles(IEnumerable<string> paths)
    {
        JobViewModel? first = null;

        foreach (var p in paths)
        {
            if (!File.Exists(p)) continue;
            if (!ImageIO.IsSupported(p)) continue;
            if (Jobs.Any(j => string.Equals(j.Job.SourcePath, p, StringComparison.OrdinalIgnoreCase))) continue;

            var vm = new JobViewModel(new CutoutJob { SourcePath = p });
            Jobs.Add(vm);
            first ??= vm;
        }

        Selected ??= first ?? Jobs.FirstOrDefault();
    }

    public void ClearFinished()
    {
        foreach (var j in Jobs.Where(j => j.Job.State is JobState.Done or JobState.Failed or JobState.Cancelled).ToList())
        {
            if (ReferenceEquals(Selected, j)) Selected = null;
            j.Dispose();
            Jobs.Remove(j);
        }

        Selected ??= Jobs.FirstOrDefault();
    }

    public bool HasJobs => Jobs.Count > 0;

    private JobViewModel? _selected;
    /// <summary>The row whose result is showing in the preview.</summary>
    public JobViewModel? Selected
    {
        get => _selected;
        set
        {
            var old = _selected;
            if (!SetField(ref _selected, value)) return;
            old?.SetSelected(false);
            value?.SetSelected(true);
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => Selected is not null;

    // ---------------------------------------------------------------- options

    public ObservableCollection<ModelRowViewModel> Models { get; } = new();

    private ModelRowViewModel _selectedModel = null!;
    public ModelRowViewModel SelectedModel
    {
        get => _selectedModel;
        set { if (SetField(ref _selectedModel, value)) OnPropertyChanged(nameof(CanStart)); }
    }

    public ObservableCollection<BackdropOption> Backdrops { get; } = new(BackdropOption.All);

    private BackdropOption _selectedBackdrop = BackdropOption.All[0];
    public BackdropOption SelectedBackdrop
    {
        get => _selectedBackdrop;
        set
        {
            if (!SetField(ref _selectedBackdrop, value)) return;
            OnPropertyChanged(nameof(WantsColour));
            OnPropertyChanged(nameof(CanSaveAsJpeg));
            if (!CanSaveAsJpeg) SaveAsJpeg = false;
        }
    }

    public bool WantsColour => SelectedBackdrop.Kind == BackdropKind.Colour;

    /// <summary>A transparent cutout has to be a PNG; JPEG has no alpha channel to put it in.</summary>
    public bool CanSaveAsJpeg => SelectedBackdrop.Kind != BackdropKind.Transparent;

    public ObservableCollection<ColourOption> Colours { get; } = new(ColourOption.All);

    private ColourOption _selectedColour = ColourOption.All[0];
    public ColourOption SelectedColour { get => _selectedColour; set => SetField(ref _selectedColour, value); }

    private bool _saveAsJpeg;
    public bool SaveAsJpeg { get => _saveAsJpeg; set => SetField(ref _saveAsJpeg, value); }

    private string? _outputFolder;
    /// <summary>Null means "beside the original", which is the default and what most people want.</summary>
    public string? OutputFolder
    {
        get => _outputFolder;
        set { if (SetField(ref _outputFolder, value)) OnPropertyChanged(nameof(OutputFolderText)); }
    }

    public string OutputFolderText => OutputFolder ?? "Beside the original file";

    // ---------------------------------------------------------------- running

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetField(ref _isRunning, value)) return;
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(NotRunning));
        }
    }

    public bool NotRunning => !IsRunning;

    public bool CanStart => !IsRunning
                            && Jobs.Any(j => j.Job.State == JobState.Waiting)
                            && SelectedModel is { IsDownloaded: true };

    /// <summary>
    /// Works through the queue one image at a time. Sequential on purpose - the ONNX session is
    /// already using every core, so two at once makes both slower.
    /// </summary>
    public async Task RunQueueAsync()
    {
        if (!CanStart) return;

        IsRunning = true;
        _cts = new CancellationTokenSource();

        var model = SelectedModel.Model;
        var modelPath = ModelCatalog.PathFor(model);
        var backdrop = SelectedBackdrop.Kind;
        var colour = SelectedColour.Value;
        var folder = OutputFolder;
        var jpeg = SaveAsJpeg && CanSaveAsJpeg;

        try
        {
            foreach (var vm in Jobs.ToList())
            {
                if (_cts.IsCancellationRequested) break;
                if (vm.Job.State != JobState.Waiting) continue;

                vm.SetState(JobState.Running, "Working out the subject…");

                try
                {
                    var token = _cts.Token;
                    var (bytes, width, height, outPath) = await Task.Run(() =>
                    {
                        using var result = _engine.Run(vm.Job.SourcePath, model, modelPath, backdrop, colour, token);

                        var path = ImageIO.NextFreePath(vm.Job.SourcePath, folder, "-cutout",
                            jpeg ? ".jpg" : ".png");
                        ImageIO.Save(result, path, jpeg);

                        // Encoded once here, on the background thread, so the UI thread only
                        // ever has to hand a byte array to the image control.
                        using var image = SKImage.FromBitmap(result);
                        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                        return (data.ToArray(), result.Width, result.Height, path);
                    }, token);

                    vm.Job.Width = width;
                    vm.Job.Height = height;
                    vm.Job.OutputPath = outPath;
                    vm.SetPreview(bytes);
                    vm.SetState(JobState.Done, "Saved " + Path.GetFileName(outPath));

                    Selected ??= vm;
                }
                catch (OperationCanceledException)
                {
                    vm.SetState(JobState.Cancelled, "Stopped before it finished.");
                }
                catch (ImageReadException ex)
                {
                    vm.SetState(JobState.Failed, ex.Message);
                }
                catch (Exception ex)
                {
                    Log.Write($"{vm.Job.FileName}: {ex}");
                    vm.SetState(JobState.Failed, ex.Message);
                }

                OnPropertyChanged(nameof(StatusLine));
            }
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
            OnPropertyChanged(nameof(StatusLine));
            OnPropertyChanged(nameof(CanStart));
        }
    }

    public void Cancel() => _cts?.Cancel();
}

/// <summary>What goes behind the subject, as the dropdown says it.</summary>
public sealed record BackdropOption(BackdropKind Kind, string Name)
{
    public override string ToString() => Name;

    public static readonly BackdropOption[] All =
    {
        new(BackdropKind.Transparent, "Nothing - transparent PNG"),
        new(BackdropKind.Colour, "A flat colour"),
        new(BackdropKind.Blur, "The original background, blurred"),
    };
}

/// <summary>A backdrop colour. Kept short - this is a cutout tool, not a paint program.</summary>
public sealed record ColourOption(string Name, SKColor Value)
{
    public override string ToString() => Name;

    public string Hex => $"#{Value.Red:X2}{Value.Green:X2}{Value.Blue:X2}";

    public static readonly ColourOption[] All =
    {
        new("White", new SKColor(0xFF, 0xFF, 0xFF)),
        new("Black", new SKColor(0x00, 0x00, 0x00)),
        new("Light grey", new SKColor(0xF2, 0xF2, 0xF2)),
        new("Passport blue", new SKColor(0xC8, 0xD8, 0xEC)),
        new("Studio navy", new SKColor(0x0A, 0x0D, 0x16)),
        new("Green screen", new SKColor(0x00, 0xB1, 0x40)),
    };
}

/// <summary>Minimal INotifyPropertyChanged, matching the hand-rolled one in DriverGeek and CleanGeek.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

/// <summary>A command with no parameter. Enough for this app; no need for a toolkit.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _run;
    private readonly Func<bool>? _can;

    public RelayCommand(Action run, Func<bool>? can = null) { _run = run; _can = can; }

    public bool CanExecute(object? parameter) => _can?.Invoke() ?? true;
    public void Execute(object? parameter) => _run();
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
