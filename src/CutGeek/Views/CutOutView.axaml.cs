using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CutGeek.ViewModels;

namespace CutGeek.Views;

public partial class CutOutView : UserControl
{
    public CutOutView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private MainWindow? Window => this.GetVisualRoot() as MainWindow;

    private async void OnAddFiles(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Window is { } w) await w.PickFilesAsync();
    }

    private async void OnPickFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Window is { } w) await w.PickFolderAsync();
    }

    /// <summary>Back to writing cutouts beside the photograph they came from.</summary>
    private void OnResetFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm) vm.OutputFolder = null;
    }

    /// <summary>
    /// Clicking a row shows it in the preview. A plain PointerPressed rather than a ListBox
    /// because the rows carry their own buttons, and a ListBox would fight them for the click.
    /// </summary>
    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: JobViewModel job }
            && DataContext is ShellViewModel vm)
        {
            vm.Selected = job;
        }
    }
}
