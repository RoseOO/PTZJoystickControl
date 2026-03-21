using PtzJoystickControl.Application.Commands;
using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Services;

namespace PtzJoystickControl.Application.Services;

public class CommandsService : ICommandsService
{
    private readonly IVmixService? _vmixService;

    public CommandsService() { }

    public CommandsService(IVmixService vmixService)
    {
        _vmixService = vmixService;
    }

    public IEnumerable<ICommand> GetCommandsForGamepad(IGamepad gamepad)
    {
        var commands = new List<ICommand>
        {
            new PanCommand(gamepad),
            new TiltCommand(gamepad),
            new ZoomCommand(gamepad),
            new FocusMoveCommand(gamepad),
            new FocusModeCommand(gamepad),
            new FocusLockCommand(gamepad),
            new PresetCommand(gamepad),
            new PresetRecallSpeedComamnd(gamepad),
            new SelectCameraCommand(gamepad),
            new PowerCommand(gamepad),
            new ExposureModeCommand(gamepad),
            new IrisCommand(gamepad),
            new ShutterCommand(gamepad),
            new GainCommand(gamepad),
            new WhiteBalanceModeCommand(gamepad),
            new BacklightCommand(gamepad),
            new RedGainCommand(gamepad),
            new BlueGainCommand(gamepad),
            new ApertureCommand(gamepad),
            new WhiteBalanceTriggerCommand(gamepad),
        };

        if (_vmixService != null)
        {
            commands.Add(new VmixCutCommand(gamepad, _vmixService));
            commands.Add(new VmixFadeCommand(gamepad, _vmixService));
        }

        return commands;
    }
}
