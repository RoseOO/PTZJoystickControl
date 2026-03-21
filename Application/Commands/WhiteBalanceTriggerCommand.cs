using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;

namespace PtzJoystickControl.Application.Commands;

public class WhiteBalanceTriggerCommand : IStaticCommand
{
    public WhiteBalanceTriggerCommand(IGamepad gamepad) : base(gamepad)
    {
    }

    public override string CommandName => "WB trigger";

    public override string AxisParameterName => "Action";

    public override string ButtonParameterName => "Action";

    public override IEnumerable<CommandValueOption> Options => optionsList;
    private static readonly IEnumerable<CommandValueOption> optionsList = new CommandValueOption[] {
        new CommandValueOption("One Push Trigger", 1),
    };

    public override void Execute(int value)
    {
        Gamepad.SelectedCamera?.TriggerWhiteBalance();
    }
}
