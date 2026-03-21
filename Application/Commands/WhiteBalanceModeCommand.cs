using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;

namespace PtzJoystickControl.Application.Commands;

public class WhiteBalanceModeCommand : IStaticCommand
{
    public WhiteBalanceModeCommand(IGamepad gamepad) : base(gamepad)
    {
    }

    public override string CommandName => "White balance mode";

    public override string AxisParameterName => "Mode";

    public override string ButtonParameterName => "Mode";

    public override IEnumerable<CommandValueOption> Options => optionsList;
    private static readonly IEnumerable<CommandValueOption> optionsList = new CommandValueOption[] {
        new CommandValueOption("Auto", (int)WhiteBalanceMode.Auto),
        new CommandValueOption("Indoor", (int)WhiteBalanceMode.Indoor),
        new CommandValueOption("Outdoor", (int)WhiteBalanceMode.Outdoor),
        new CommandValueOption("One Push", (int)WhiteBalanceMode.OnePush),
        new CommandValueOption("Manual", (int)WhiteBalanceMode.Manual),
    };

    public override void Execute(int value)
    {
        byte byteVal = (byte)value;
        if (Enum.IsDefined((WhiteBalanceMode)byteVal))
            Gamepad.SelectedCamera?.SetWhiteBalanceMode((WhiteBalanceMode)byteVal);
        else
            throw new ArgumentException("Invalid value. Must be one of enum WhiteBalanceMode.");
    }
}
