using System.Collections.Generic;

namespace PtzJoystickControl.Core.Model;

public class FullConfigExport
{
    public List<ViscaDeviceSettings>? Cameras { get; set; }
    public VmixSettings? VmixSettings { get; set; }
    public List<GamepadSettings>? GamepadSettings { get; set; }
    public string? ExportedAt { get; set; }
    public string? Version { get; set; }
}
