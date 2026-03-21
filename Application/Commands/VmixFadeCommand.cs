using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;
using PtzJoystickControl.Core.Services;

namespace PtzJoystickControl.Application.Commands;

public class VmixFadeCommand : IStaticCommand
{
    private readonly IVmixService _vmixService;

    public VmixFadeCommand(IGamepad gamepad, IVmixService vmixService) : base(gamepad)
    {
        _vmixService = vmixService;
    }

    public override string CommandName => "vMix Fade";

    public override string AxisParameterName => "Duration (ms)";

    public override string ButtonParameterName => "Duration (ms)";

    public override IEnumerable<CommandValueOption> Options => optionsList;
    private static readonly IEnumerable<CommandValueOption> optionsList = new CommandValueOption[] {
        new CommandValueOption("500ms", 500),
        new CommandValueOption("1000ms", 1000),
        new CommandValueOption("1500ms", 1500),
        new CommandValueOption("2000ms", 2000),
        new CommandValueOption("3000ms", 3000),
    };

    public override void Execute(int value)
    {
        if (value <= 0) value = 1000;
        _ = _vmixService.SendFadeAsync(value);
    }
}
