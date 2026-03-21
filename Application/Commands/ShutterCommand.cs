using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;

namespace PtzJoystickControl.Application.Commands;

public class ShutterCommand : IStaticCommand
{
    public ShutterCommand(IGamepad gamepad) : base(gamepad)
    {
    }

    public override string CommandName => "Shutter";

    public override string AxisParameterName => "Action";

    public override string ButtonParameterName => "Action";

    public override IEnumerable<CommandValueOption> Options => optionsList;
    private static readonly IEnumerable<CommandValueOption> optionsList = new CommandValueOption[] {
        new CommandValueOption("Reset", (int)ShutterDir.Reset),
        new CommandValueOption("Up (Faster)", (int)ShutterDir.Up),
        new CommandValueOption("Down (Slower)", (int)ShutterDir.Down),
    };

    public override void Execute(int value)
    {
        byte byteVal = (byte)value;
        if (Enum.IsDefined((ShutterDir)byteVal))
            Gamepad.SelectedCamera?.AdjustShutter((ShutterDir)byteVal);
        else
            throw new ArgumentException("Invalid value. Must be one of enum ShutterDir.");
    }
}
