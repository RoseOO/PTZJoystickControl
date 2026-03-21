using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;

namespace PtzJoystickControl.Application.Commands;

public class ExposureModeCommand : IStaticCommand
{
    public ExposureModeCommand(IGamepad gamepad) : base(gamepad)
    {
    }

    public override string CommandName => "Exposure mode";

    public override string AxisParameterName => "Mode";

    public override string ButtonParameterName => "Mode";

    public override IEnumerable<CommandValueOption> Options => optionsList;
    private static readonly IEnumerable<CommandValueOption> optionsList = new CommandValueOption[] {
        new CommandValueOption("Auto", (int)ExposureMode.Auto),
        new CommandValueOption("Manual", (int)ExposureMode.Manual),
        new CommandValueOption("Shutter Priority", (int)ExposureMode.ShutterPriority),
        new CommandValueOption("Iris Priority", (int)ExposureMode.IrisPriority),
        new CommandValueOption("Bright", (int)ExposureMode.Bright),
    };

    public override void Execute(int value)
    {
        byte byteVal = (byte)value;
        if (Enum.IsDefined((ExposureMode)byteVal))
            Gamepad.SelectedCamera?.SetExposureMode((ExposureMode)byteVal);
        else
            throw new ArgumentException("Invalid value. Must be one of enum ExposureMode.");
    }
}
