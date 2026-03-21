using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace PtzJoystickControl.Gui.Views;

public partial class CameraOverlayWindow : Window
{
    public CameraOverlayWindow()
    {
        InitializeComponent();

        // Enable dragging the window by clicking and dragging anywhere on it
        PointerPressed += OnPointerPressed;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
