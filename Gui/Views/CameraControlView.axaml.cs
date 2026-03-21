using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PtzJoystickControl.Gui.Views;

public partial class CameraControlView : UserControl
{
    public CameraControlView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
