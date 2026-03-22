namespace PtzJoystickControl.Core.Services;

public interface IVmixService
{
    bool IsConnected { get; }
    string Host { get; set; }
    int Port { get; set; }
    bool AutoPreview { get; set; }
    bool AutoCameraSelect { get; set; }

    event Action<int>? PreviewInputChanged;

    Task ConnectAsync();
    void Disconnect();
    Task SendPreviewInputAsync(int inputNumber);
    Task SendCutAsync();
    Task SendFadeAsync(int duration);
}
