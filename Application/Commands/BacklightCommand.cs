using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;

namespace PtzJoystickControl.Application.Commands;

public class BacklightCommand : IStaticCommand
{
    public BacklightCommand(IGamepad gamepad) : base(gamepad)
    {
    }

    public override string CommandName => "Backlight compensation";

    public override string AxisParameterName => "Action";

    public override string ButtonParameterName => "Action";

    public override IEnumerable<CommandValueOption> Options => optionsList;
    private static readonly IEnumerable<CommandValueOption> optionsList = new CommandValueOption[] {
        new CommandValueOption("On", (int)BacklightCompensation.On),
        new CommandValueOption("Off", (int)BacklightCompensation.Off),
    };

    public override void Execute(int value)
    {
        byte byteVal = (byte)value;
        if (Enum.IsDefined((BacklightCompensation)byteVal))
            Gamepad.SelectedCamera?.SetBacklightCompensation((BacklightCompensation)byteVal);
        else
            throw new ArgumentException("Invalid value. Must be one of enum BacklightCompensation.");
    }
}
