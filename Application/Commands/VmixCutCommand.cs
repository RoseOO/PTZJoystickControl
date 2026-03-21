using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;
using PtzJoystickControl.Core.Services;

namespace PtzJoystickControl.Application.Commands;

public class VmixCutCommand : IStaticCommand
{
    private readonly IVmixService _vmixService;

    public VmixCutCommand(IGamepad gamepad, IVmixService vmixService) : base(gamepad)
    {
        _vmixService = vmixService;
    }

    public override string CommandName => "vMix Cut";

    public override string AxisParameterName => "Action";

    public override string ButtonParameterName => "Action";

    public override IEnumerable<CommandValueOption> Options => optionsList;
    private static readonly IEnumerable<CommandValueOption> optionsList = new CommandValueOption[] {
        new CommandValueOption("Cut", 1),
    };

    public override void Execute(int value)
    {
        _ = _vmixService.SendCutAsync();
    }
}
