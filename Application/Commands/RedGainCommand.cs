using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;

namespace PtzJoystickControl.Application.Commands;

public class RedGainCommand : IStaticCommand
{
    public RedGainCommand(IGamepad gamepad) : base(gamepad)
    {
    }

    public override string CommandName => "Red gain";

    public override string AxisParameterName => "Action";

    public override string ButtonParameterName => "Action";

    public override IEnumerable<CommandValueOption> Options => optionsList;
    private static readonly IEnumerable<CommandValueOption> optionsList = new CommandValueOption[] {
        new CommandValueOption("Reset", (int)GainDir.Reset),
        new CommandValueOption("Up", (int)GainDir.Up),
        new CommandValueOption("Down", (int)GainDir.Down),
    };

    public override void Execute(int value)
    {
        byte byteVal = (byte)value;
        if (Enum.IsDefined((GainDir)byteVal))
            Gamepad.SelectedCamera?.AdjustRedGain((GainDir)byteVal);
        else
            throw new ArgumentException("Invalid value. Must be one of enum GainDir.");
    }
}
