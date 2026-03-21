namespace PtzJoystickControl.Core.Model;

public class VmixSettings
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8088;
    public bool Enabled { get; set; }
    public bool AutoPreview { get; set; } = true;
    public Dictionary<int, int> CameraToVmixInput { get; set; } = new();
}
