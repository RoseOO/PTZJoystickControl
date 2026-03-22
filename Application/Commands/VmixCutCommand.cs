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
    // Value encoding: lower 16 bits unused, upper 16 bits = mix number (0 = default/no mix)
    private static readonly IEnumerable<CommandValueOption> optionsList = new CommandValueOption[] {
        new CommandValueOption("Cut", 0),
        new CommandValueOption("Cut (Mix 2)", 2),
        new CommandValueOption("Cut (Mix 3)", 3),
        new CommandValueOption("Cut (Mix 4)", 4),
    };

    public override void Execute(int value)
    {
        int? mix = value > 0 ? value : null;
        _ = _vmixService.SendCutAsync(mix);
    }
}
