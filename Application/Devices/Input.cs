using PtzJoystickControl.Core.Commands;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Application.Commands;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PtzJoystickControl.Core.Model;
using System.Collections.Specialized;

namespace PtzJoystickControl.Application.Devices;

public class Input : IInput
{
    public string Name { get; private set; }
    public InputType InputType { get; private set; }
    public string Id { get; private set; }

    private float inputValue;
    private float rawInputValue;
    private ICommand? command;
    private CommandValueOption? commandValue;
    private CommandDirectionOption? commandDirection;
    private Direction currentDirection;
    private readonly IInput? secondInput;
    private int currentValue;
    private bool buttonPressed { get; set; }
    private float deadZone { get; set; } = 0.05F;
    private float saturation { get; set; } = 0.05F;
    private bool inverted { get; set; }
    private bool defaultCenter { get; set; } = true;
    private bool enableRamping { get; set; } = false;
    private float rampTime { get; set; } = 0.3F; // 300ms default ramp time

    // Ramping state
    private float targetValue = 0;
    private float currentRampedValue = 0;
    private Direction lastRampDirection = Direction.Stop;
    private DateTime lastUpdateTime = DateTime.UtcNow;
    private Timer? _rampTimer;
    private const int RampTimerIntervalMs = 20;
    private readonly object _executeLock = new();

    private Input(string id, string name, InputType inputType, IEnumerable<ICommand> commands, bool isSecondInput)
    {
        Id = id;
        Name = name;
        InputType = inputType;
        Commands = commands;
        SecondInput = InputType == InputType.Axis && !isSecondInput ? new Input(string.Empty, string.Empty, InputType.Axis, commands, true) : null;
    }

    public Input(string id, string name, InputType inputType, IEnumerable<ICommand> commands) : this(id, name, inputType, commands, false)
    {
    }

    public Input(string id, string name, InputType inputType, IEnumerable<ICommand> commands, int value) : this(id, name, inputType, commands)
    {
        InputValue = value;
    }

    public Input(string id, string name, InputType inputType, IEnumerable<ICommand> commands, int value, ICommand command) : this(id, name, inputType, commands, value)
    {
        SelectedCommand = command;
    }

    public float DeadZone
    {
        get => deadZone;
        set
        {
            deadZone = value;
            NotifyPersistentPropertyChanged();
        }
    }
    public float Saturation
    {
        get => saturation;
        set
        {
            saturation = value;
            NotifyPersistentPropertyChanged();
        }
    }

    public bool Inverted
    {
        get => inverted;
        set
        {
            inverted = value;
            NotifyPersistentPropertyChanged();
        }
    }

    public bool DefaultCenter
    {
        get => defaultCenter;
        set
        {
            defaultCenter = value;
            NotifyPersistentPropertyChanged();
        }
    }

    public bool EnableRamping
    {
        get => enableRamping;
        set
        {
            enableRamping = value;
            if (!value)
            {
                // Reset ramping state when disabled
                currentRampedValue = 0;
                targetValue = 0;
                StopRampTimer();
            }
            NotifyPersistentPropertyChanged();
        }
    }

    public float RampTime
    {
        get => rampTime;
        set
        {
            rampTime = Math.Max(0.05F, Math.Min(value, 2.0F)); // Clamp between 50ms and 2s
            NotifyPersistentPropertyChanged();
        }
    }

    public Direction CurrentDirection
    {
        get => currentDirection;
        private set
        {
            currentDirection = value;
            NotifyPropertyChanged();
        }
    }
    public int CurrentValue
    {
        get => currentValue;
        private set
        {
            currentValue = value;
            NotifyPropertyChanged();
        }
    }

    public IEnumerable<ICommand> Commands { get; private set; }

    public float InputValue
    {
        get => inputValue;
        set
        {
            inputValue = -1 <= value && value <= 1 ? value : throw new ArgumentOutOfRangeException($"Value must be between -1 and 1, but was {value}");
            RawInputValue = inputValue;
            if (!defaultCenter) inputValue = Util.Map(inputValue, -1, 1, 0, 1);
            var isNegative = inputValue < 0;
            var absVal = Math.Abs(inputValue);

            if (absVal <= DeadZone) inputValue = 0;
            else if (absVal >= 1 - Saturation) inputValue = 1;
            else inputValue = Util.Map(absVal, DeadZone, 1 - Saturation, 0, 1);

            if (isNegative ^ inverted) inputValue = -inputValue;

            ExecuteCommand();
            NotifyPropertyChanged();
        }
    }

    public float RawInputValue
    {
        get => rawInputValue;
        private set
        {
            rawInputValue = value;
            NotifyPropertyChanged();
        }
    }

    public CommandValueOption? CommandValue
    {
        get => commandValue;
        set
        {
            commandValue = value;
            NotifyPersistentPropertyChanged();
        }
    }

    public CommandDirectionOption? CommandDirection
    {
        get => commandDirection;
        set
        {
            commandDirection = value;
            if(secondInput != null) 
                 secondInput.CommandDirection = CommandDirection?.Direction == Direction.High 
                    ? staticDirections.First(d => d.Direction == Direction.Low)
                    : staticDirections.First(d => d.Direction == Direction.High);
            NotifyPersistentPropertyChanged();
        }
    }

    public ICommand? SelectedCommand
    {
        get => command;
        set
        {
            //Hack: Detect change of options for SelectCameraCommand
            if (command != null && command is SelectCameraCommand oldCommand)
            {
                oldCommand.CollectionChanged -= OnSelectCameraCommandCollectionChanged;
                oldCommand.PropertyChanged -= OnSelectCameraCommandPropertyChanged;
            }
            command = value;
            if (command != null && command is SelectCameraCommand newCommand)
            {
                newCommand.CollectionChanged += OnSelectCameraCommandCollectionChanged;
                newCommand.PropertyChanged += OnSelectCameraCommandPropertyChanged;
            }

            if(!(InputType == InputType.Axis && secondInput == null))
                CommandDirection = Directions?.FirstOrDefault();

            CommandValue = command is IStaticCommand ? Values?.FirstOrDefault() : Values?.LastOrDefault();
            NotifyPersistentPropertyChanged();
        }
    }

    public IEnumerable<CommandValueOption>? Values { get => command?.Options; }

    private static CommandDirectionOption[] staticDirections = new CommandDirectionOption[] {
                    new CommandDirectionOption("High", Direction.High),
                    new CommandDirectionOption("Low", Direction.Low)
                };

    public IEnumerable<CommandDirectionOption>? Directions
    {
        get
        {
            if (command is IDynamicCommand dynCommand) return dynCommand.ButtonDirections;
            else if (command is IStaticCommand) return staticDirections;
            return null;
        }
    }

    public IInput? SecondInput {
        get => secondInput;
        private init {
            secondInput = value;
            if(secondInput != null) {
                secondInput.DeadZone = 0;
                secondInput.Saturation = 0;
                secondInput.CommandDirection = CommandDirection?.Direction == Direction.High 
                    ? staticDirections.First(d => d.Direction == Direction.Low)
                    : staticDirections.First(d => d.Direction == Direction.High);
                secondInput.PersistentPropertyChanged += PersistentPropertyChanged;
            }
        }
    }

    private void ExecuteCommand()
    {
        lock (_executeLock)
        {
        if (command is IDynamicCommand dynCommand)
        {
            if (InputType == InputType.Axis)
            {
                int maxValue = CommandValue?.Value ?? dynCommand.MaxValue;
                int range = maxValue - dynCommand.MinValue;

                float mappedValue = Util.Map(InputValue, -1, 1, -range, range);

                CurrentDirection = Direction.Stop;

                // Special handling for non-centered inputs (like triggers)
                if (!defaultCenter)
                {
                    // For triggers/non-centered axes, map 0-1 to min-max range
                    // and use configured direction
                    if (InputValue > 0)
                    {
                        mappedValue = Util.Map(InputValue, 0, 1, dynCommand.MinValue, maxValue);
                        CurrentDirection = CommandDirection?.Direction ?? Direction.High;

                        if (enableRamping)
                        {
                            mappedValue = ApplyRamping(mappedValue, dynCommand.MinValue, maxValue);
                        }

                        CurrentValue = (int)mappedValue;
                        dynCommand.Execute((int)mappedValue, CurrentDirection);
                    }
                    else
                    {
                        // Trigger released - ramp down if enabled, otherwise stop immediately
                        if (enableRamping)
                        {
                            mappedValue = ApplyRamping(0, dynCommand.MinValue, maxValue);
                            if (mappedValue > dynCommand.MinValue)
                            {
                                CurrentDirection = CommandDirection?.Direction ?? Direction.High;
                                CurrentValue = (int)mappedValue;
                                dynCommand.Execute((int)mappedValue, CurrentDirection);
                            }
                            else
                            {
                                CurrentValue = 0;
                                currentRampedValue = 0;
                                dynCommand.Execute(0, Direction.Stop);
                            }
                        }
                        else
                        {
                            CurrentValue = 0;
                            dynCommand.Execute(0, Direction.Stop);
                        }
                    }
                }
                else
                {
                    // Standard centered axis behavior (joysticks) with optional ramping
                    float finalValue = mappedValue;

                    if (enableRamping)
                    {
                        // For centered axes, ramp the absolute value then reapply direction
                        targetValue = Math.Abs(mappedValue);
                        finalValue = ApplyRamping(targetValue, dynCommand.MinValue, range);

                        // Track direction: update when actually moving, keep last direction during ramp-down
                        if (mappedValue < 0)
                            lastRampDirection = Direction.Low;
                        else if (mappedValue > 0)
                            lastRampDirection = Direction.High;
                        // When mappedValue == 0 (released), keep lastRampDirection for ramp-down

                        // Apply tracked direction to the ramped value
                        if (lastRampDirection == Direction.Low)
                            finalValue = -finalValue;
                    }

                    if (finalValue < 0)
                    {
                        finalValue = Math.Abs(finalValue) + dynCommand.MinValue;
                        CurrentDirection = Direction.Low;
                        CurrentValue = (int)finalValue;
                        dynCommand.Execute((int)finalValue, CurrentDirection);
                    }
                    else if (finalValue > 0)
                    {
                        finalValue += dynCommand.MinValue;
                        CurrentDirection = Direction.High;
                        CurrentValue = (int)finalValue;
                        dynCommand.Execute((int)finalValue, CurrentDirection);
                    }
                    else
                    {
                        CurrentValue = 0;
                        currentRampedValue = 0;
                        lastRampDirection = Direction.Stop;
                        dynCommand.Execute(0, Direction.Stop);
                    }
                }
            }
            else if (InputType == InputType.Button)
            {
                if (inputValue != 0) dynCommand.Execute(commandValue!, CommandDirection!);
                else dynCommand.Execute(0, Direction.Stop);
            }
        }
        else if (command is IStaticCommand statCommand)
        {
            //Execute static command when Button goes from unpressed to pressed only
            bool executeCommand = false;

            CurrentDirection = Direction.Stop;

            if (InputType == InputType.Axis)
            {
                if (buttonPressed && (
                        (CommandDirection?.Direction == Direction.High && 0.25 > inputValue) ||
                        (CommandDirection?.Direction == Direction.Low && -0.25 < inputValue)))
                {
                    buttonPressed = false;
                    CurrentValue = 0;
                }
                else if (!buttonPressed && (
                        (CommandDirection?.Direction == Direction.High && inputValue > 0.25) ||
                        (CommandDirection?.Direction == Direction.Low && inputValue < -0.25)))
                {
                    buttonPressed = true;
                    executeCommand = true;
                    CurrentValue = commandValue!.Value;
                }

                if(secondInput != null)
                    secondInput.InputValue = commandDirection?.Direction == Direction.High ? Math.Min(inputValue, 0) : Math.Max(inputValue, 0);
            }
            else if (InputType == InputType.Button)
            {
                if (buttonPressed && inputValue == 0)
                    buttonPressed = false;
                else if (!buttonPressed && inputValue != 0)
                {
                    buttonPressed = true;
                    executeCommand = true;
                }
            }

            if (executeCommand) statCommand.Execute(commandValue!);
        }
        } // lock
    }

    private float ApplyRamping(float targetValue, float minValue, float maxValue)
    {
        DateTime now = DateTime.UtcNow;
        float deltaTime = (float)(now - lastUpdateTime).TotalSeconds;
        lastUpdateTime = now;

        // Cap deltaTime to prevent large jumps after long gaps between events
        deltaTime = Math.Min(deltaTime, 0.05f);

        // Calculate how much we can change per second
        float rampSpeed = (maxValue - minValue) / rampTime;
        float maxChange = rampSpeed * deltaTime;

        // Move current ramped value toward target
        float delta = targetValue - currentRampedValue;

        if (Math.Abs(delta) <= maxChange)
        {
            currentRampedValue = targetValue;
        }
        else
        {
            currentRampedValue += Math.Sign(delta) * maxChange;
        }

        // Start or stop ramp timer based on whether ramping is complete
        if (Math.Abs(targetValue - currentRampedValue) > 0.001f)
            StartRampTimer();
        else
            StopRampTimer();

        return currentRampedValue;
    }

    private void StartRampTimer()
    {
        if (_rampTimer == null)
        {
            _rampTimer = new Timer(OnRampTimerTick, null, RampTimerIntervalMs, RampTimerIntervalMs);
        }
    }

    private void StopRampTimer()
    {
        _rampTimer?.Dispose();
        _rampTimer = null;
    }

    private void OnRampTimerTick(object? state)
    {
        if (!enableRamping || command == null)
        {
            StopRampTimer();
            return;
        }

        // Re-execute the command to continue ramping
        ExecuteCommand();
    }

    public event PropertyChangedEventHandler? PersistentPropertyChanged;
    private void NotifyPersistentPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PersistentPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        NotifyPropertyChanged(propertyName);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OnSelectCameraCommandCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (CommandValue! != null! && e?.OldStartingIndex >= 0)
        {
            if (CommandValue.Value == e.OldStartingIndex)
                CommandValue = null;
            else if (CommandValue.Value > e.OldStartingIndex)
                CommandValue = Values?.ElementAtOrDefault(CommandValue.Value - 1);
        }
        NotifyPropertyChanged(nameof(Values));
        NotifyPropertyChanged(nameof(CommandValue));
    }

    private void OnSelectCameraCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var UpdatedValue = Values?.ElementAtOrDefault(CommandValue?.Value ?? -1);
        if (UpdatedValue?.Name != CommandValue?.Name)
            CommandValue = UpdatedValue;
        NotifyPropertyChanged(nameof(Values));
        NotifyPropertyChanged(nameof(CommandValue));
    }
}
