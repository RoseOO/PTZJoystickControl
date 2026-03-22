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
    // Value encoding: lower 16 bits = duration, upper 16 bits = mix number (0 = default/no mix)
    private static readonly IEnumerable<CommandValueOption> optionsList = BuildOptions().ToArray();

    private static IEnumerable<CommandValueOption> BuildOptions()
    {
        int[] durations = { 500, 1000, 1500, 2000, 3000 };
        int[] mixes = { 0, 2, 3, 4 };

        foreach (int mix in mixes)
        {
            foreach (int duration in durations)
            {
                int encodedValue = (mix << 16) | duration;
                string name = mix == 0
                    ? $"{duration}ms"
                    : $"{duration}ms (Mix {mix})";
                yield return new CommandValueOption(name, encodedValue);
            }
        }
    }

    public override void Execute(int value)
    {
        int duration = value & 0xFFFF;
        int mix = value >> 16;
        if (duration <= 0) duration = 1000;
        int? mixParam = mix > 0 ? mix : null;
        _ = _vmixService.SendFadeAsync(duration, mixParam);
    }
}
