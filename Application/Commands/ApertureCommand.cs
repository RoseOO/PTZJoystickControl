using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;

namespace PtzJoystickControl.Application.Commands;

public class ApertureCommand : IStaticCommand
{
    public ApertureCommand(IGamepad gamepad) : base(gamepad)
    {
    }

    public override string CommandName => "Aperture";

    public override string AxisParameterName => "Action";

    public override string ButtonParameterName => "Action";

    public override IEnumerable<CommandValueOption> Options => optionsList;
    private static readonly IEnumerable<CommandValueOption> optionsList = new CommandValueOption[] {
        new CommandValueOption("Reset", (int)ApertureDir.Reset),
        new CommandValueOption("Up", (int)ApertureDir.Up),
        new CommandValueOption("Down", (int)ApertureDir.Down),
    };

    public override void Execute(int value)
    {
        byte byteVal = (byte)value;
        if (Enum.IsDefined((ApertureDir)byteVal))
            Gamepad.SelectedCamera?.AdjustAperture((ApertureDir)byteVal);
        else
            throw new ArgumentException("Invalid value. Must be one of enum ApertureDir.");
    }
}
