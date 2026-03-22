using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PtzJoystickControl.Gui.Views;

public partial class TcpSerialCameraView : UserControl
{
    public TcpSerialCameraView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
