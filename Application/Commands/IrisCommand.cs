using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;

namespace PtzJoystickControl.Application.Commands;

public class IrisCommand : IStaticCommand
{
    public IrisCommand(IGamepad gamepad) : base(gamepad)
    {
    }

    public override string CommandName => "Iris";

    public override string AxisParameterName => "Action";

    public override string ButtonParameterName => "Action";

    public override IEnumerable<CommandValueOption> Options => optionsList;
    private static readonly IEnumerable<CommandValueOption> optionsList = new CommandValueOption[] {
        new CommandValueOption("Reset", (int)IrisDir.Reset),
        new CommandValueOption("Up", (int)IrisDir.Up),
        new CommandValueOption("Down", (int)IrisDir.Down),
    };

    public override void Execute(int value)
    {
        byte byteVal = (byte)value;
        if (Enum.IsDefined((IrisDir)byteVal))
            Gamepad.SelectedCamera?.AdjustIris((IrisDir)byteVal);
        else
            throw new ArgumentException("Invalid value. Must be one of enum IrisDir.");
    }
}
