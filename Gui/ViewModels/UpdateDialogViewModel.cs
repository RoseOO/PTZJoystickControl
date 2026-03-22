using PtzJoystickControl.Core.Model;
using System.Diagnostics;
using Avalonia.Layout;
using System.Runtime.InteropServices;

namespace PtzJoystickControl.Gui.ViewModels;

public class UpdateDialogViewModel : ViewModelBase
{
    private readonly Update _update;
    public UpdateDialogViewModel(Update upate)
    {
        _update = upate;
    }

    public bool AcrylicEnabled => false; // UpdateDialog doesn't need acrylic blur
    public HorizontalAlignment TitleHorizontalAlignment => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
        ? HorizontalAlignment.Left 
        : HorizontalAlignment.Center;

    public bool IsAvailable { get => _update.Available; }
    public string? LatestVersion { get => _update.Version; }
    public bool HasLatestVersion { get => !string.IsNullOrEmpty(_update.Version); }
    public bool Error { get => _update.Error; }
    public string? ErrorMessage { get => _update.ErrorMessage; }
    public bool NotAvailableOrError { get => !(IsAvailable || Error); }
    public void Download()
    {
        if (_update.Uri != null) Process.Start(new ProcessStartInfo
        {
            FileName = _update.Uri.ToString(),
            UseShellExecute = true
        });
    }
}
