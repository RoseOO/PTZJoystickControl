using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Gui.ViewModels;

namespace PtzJoystickControl.Gui.Views;

public partial class CameraControlView : UserControl
{
    private Canvas? _joystickCanvas;
    private Ellipse? _joystickThumb;
    private bool _joystickDragging;

    private const double JoystickSize = 80;
    private const double ThumbSize = 20;
    private const double Center = JoystickSize / 2;
    private const double MaxRadius = (JoystickSize - ThumbSize) / 2;

    private const int RampUpdateIntervalMs = 30;
    private const float MinimumSpeedThreshold = 0.5f;

    // Ramping state
    private float _targetPanSpeed;
    private float _targetTiltSpeed;
    private float _currentPanSpeed;
    private float _currentTiltSpeed;
    private PanDir _targetPanDir = PanDir.Stop;
    private TiltDir _targetTiltDir = TiltDir.Stop;
    private DateTime _lastRampUpdate = DateTime.UtcNow;
    private DispatcherTimer? _rampTimer;
    private volatile bool _rampActive;

    // Track the currently pressed PTZ button tag for reliable stop-on-release
    private string? _activePtzButtonTag;

    public CameraControlView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Stop ramping when DataContext changes
        StopRampTimer();
        _currentPanSpeed = 0;
        _currentTiltSpeed = 0;
        _targetPanSpeed = 0;
        _targetTiltSpeed = 0;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Register with handledEventsToo so we get the events
        // even after Button controls mark them as handled
        this.AddHandler(PointerPressedEvent, OnPtzButtonPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        this.AddHandler(PointerReleasedEvent, OnPtzButtonReleased, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Cleanup timer when control is removed
        StopRampTimer();
        this.RemoveHandler(PointerPressedEvent, OnPtzButtonPressed);
        this.RemoveHandler(PointerReleasedEvent, OnPtzButtonReleased);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _joystickCanvas = this.FindControl<Canvas>("JoystickCanvas");
        _joystickThumb = this.FindControl<Ellipse>("JoystickThumb");
    }

    private void OnPtzButtonPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not CameraControlViewModel vm)
            return;

        // Walk up from the event source to find the tagged Button
        string? tag = null;
        if (e.Source is IControl source)
        {
            var current = source;
            while (current != null && current != this)
            {
                if (current is Control c && c.Tag is string t && !string.IsNullOrEmpty(t))
                {
                    tag = t;
                    break;
                }
                current = current.Parent as IControl;
            }
        }

        if (string.IsNullOrEmpty(tag))
            return;

        // Track the active button for reliable stop-on-release
        // (on-screen buttons send commands directly without ramping)
        _activePtzButtonTag = tag;

        switch (tag)
        {
            case "MoveUpLeft": vm.MoveUpLeft(); break;
            case "TiltUp": vm.TiltUp(); break;
            case "MoveUpRight": vm.MoveUpRight(); break;
            case "PanLeft": vm.PanLeft(); break;
            case "PanRight": vm.PanRight(); break;
            case "MoveDownLeft": vm.MoveDownLeft(); break;
            case "TiltDown": vm.TiltDown(); break;
            case "MoveDownRight": vm.MoveDownRight(); break;
            case "ZoomIn": vm.ZoomIn(); break;
            case "ZoomOut": vm.ZoomOut(); break;
            case "FocusFar": vm.FocusFar(); break;
            case "FocusNear": vm.FocusNear(); break;
            case "MoveStopBtn": vm.MoveStop(); break;
            case "ZoomStopBtn": vm.ZoomStop(); break;
            case "FocusStopBtn": vm.FocusStop(); break;
        }
    }

    private void OnPtzButtonReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not CameraControlViewModel vm)
            return;

        // Use the tracked active button tag for reliable stop-on-release.
        // This avoids issues with the visual tree walk failing when pointer
        // is captured by the Button control.
        var tag = _activePtzButtonTag;
        if (tag == null)
            return;

        _activePtzButtonTag = null;

        switch (tag)
        {
            case "MoveUpLeft":
            case "TiltUp":
            case "MoveUpRight":
            case "PanLeft":
            case "PanRight":
            case "MoveDownLeft":
            case "TiltDown":
            case "MoveDownRight":
                vm.MoveStop();
                break;
            case "ZoomIn":
            case "ZoomOut":
                vm.ZoomStop();
                break;
            case "FocusFar":
            case "FocusNear":
                vm.FocusStop();
                break;
        }
    }

    private void OnJoystickPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_joystickCanvas == null) return;
        _joystickDragging = true;
        _lastRampUpdate = DateTime.UtcNow;
        e.Pointer.Capture(_joystickCanvas);
        UpdateJoystickPosition(e.GetPosition(_joystickCanvas));
    }

    private void OnJoystickMoved(object? sender, PointerEventArgs e)
    {
        if (!_joystickDragging || _joystickCanvas == null) return;
        UpdateJoystickPosition(e.GetPosition(_joystickCanvas));
    }

    private void OnJoystickReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_joystickDragging) return;
        _joystickDragging = false;
        e.Pointer.Capture(null);
        ResetJoystickThumb();

        if (DataContext is not CameraControlViewModel vm || vm.Camera == null)
        {
            StopRampTimer();
            return;
        }

        if (vm.JoystickRampingEnabled)
        {
            // Set targets to zero for ramp out; timer will handle the deceleration
            _targetPanSpeed = 0;
            _targetTiltSpeed = 0;
            _targetPanDir = PanDir.Stop;
            _targetTiltDir = TiltDir.Stop;
            StartRampTimer();
        }
        else
        {
            StopRampTimer();
            _currentPanSpeed = 0;
            _currentTiltSpeed = 0;
            vm.MoveStop();
        }
    }

    private void UpdateJoystickPosition(Point pos)
    {
        if (DataContext is not CameraControlViewModel vm || vm.Camera == null)
        {
            StopRampTimer();
            return;
        }

        double dx = pos.X - Center;
        double dy = pos.Y - Center;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        // Clamp to circle boundary
        if (distance > MaxRadius)
        {
            dx = dx / distance * MaxRadius;
            dy = dy / distance * MaxRadius;
            distance = MaxRadius;
        }

        // Update thumb visual position
        if (_joystickThumb != null)
        {
            Canvas.SetLeft(_joystickThumb, Center + dx - ThumbSize / 2);
            Canvas.SetTop(_joystickThumb, Center + dy - ThumbSize / 2);
        }

        // Convert to normalized -1..1 range
        double normX = dx / MaxRadius;
        double normY = -dy / MaxRadius; // Invert Y: up is positive

        // Calculate target speeds
        float targetPan = (float)(Math.Abs(normX) * vm.PanSpeed);
        float targetTilt = (float)(Math.Abs(normY) * vm.TiltSpeed);

        var panDir = normX > 0.1 ? PanDir.Right :
                     normX < -0.1 ? PanDir.Left :
                     PanDir.Stop;
        var tiltDir = normY > 0.1 ? TiltDir.Up :
                      normY < -0.1 ? TiltDir.Down :
                      TiltDir.Stop;

        _targetPanSpeed = panDir == PanDir.Stop ? 0 : targetPan;
        _targetTiltSpeed = tiltDir == TiltDir.Stop ? 0 : targetTilt;
        _targetPanDir = panDir;
        _targetTiltDir = tiltDir;

        if (vm.JoystickRampingEnabled)
        {
            // Let the ramp timer handle sending commands
            StartRampTimer();
        }
        else
        {
            // Direct mode: send immediately
            _currentPanSpeed = _targetPanSpeed;
            _currentTiltSpeed = _targetTiltSpeed;
            SendPanTiltCommand(vm);
        }
    }

    private void StartRampTimer()
    {
        if (_rampActive) return;
        _rampActive = true;
        _lastRampUpdate = DateTime.UtcNow;
        _rampTimer?.Stop();
        _rampTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RampUpdateIntervalMs) };
        _rampTimer.Tick += RampTimerTick;
        _rampTimer.Start();
    }

    private void StopRampTimer()
    {
        _rampActive = false;
        if (_rampTimer != null)
        {
            _rampTimer.Stop();
            _rampTimer.Tick -= RampTimerTick;
            _rampTimer = null;
        }
    }

    private void RampTimerTick(object? sender, EventArgs e)
    {
        try
        {
            DateTime now = DateTime.UtcNow;
            float deltaTime = (float)(now - _lastRampUpdate).TotalSeconds;
            _lastRampUpdate = now;

            if (DataContext is not CameraControlViewModel vm || vm.Camera == null)
            {
                StopRampTimer();
                return;
            }

            float rampTime = vm.JoystickRampTime;
            if (rampTime <= 0) rampTime = 0.05f;

            // Ramp pan speed toward target
            float panRampSpeed = vm.PanSpeed / rampTime;
            _currentPanSpeed = RampValue(_currentPanSpeed, _targetPanSpeed, panRampSpeed * deltaTime);

            // Ramp tilt speed toward target
            float tiltRampSpeed = vm.TiltSpeed / rampTime;
            _currentTiltSpeed = RampValue(_currentTiltSpeed, _targetTiltSpeed, tiltRampSpeed * deltaTime);

            // Check if ramping is complete and we've stopped
            bool stopped = _currentPanSpeed < MinimumSpeedThreshold && _currentTiltSpeed < MinimumSpeedThreshold
                           && _targetPanSpeed < MinimumSpeedThreshold && _targetTiltSpeed < MinimumSpeedThreshold;

            if (stopped)
            {
                _currentPanSpeed = 0;
                _currentTiltSpeed = 0;
                vm.MoveStop();
                StopRampTimer();
                return;
            }

            // Send the ramped command (already on UI thread via DispatcherTimer)
            SendPanTiltCommand(vm);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[CameraControlView] RampTimerTick Error: {ex.Message}");
            StopRampTimer();
        }
    }

    private static float RampValue(float current, float target, float maxChange)
    {
        float delta = target - current;
        if (Math.Abs(delta) <= maxChange)
            return target;
        return current + Math.Sign(delta) * maxChange;
    }

    private void SendPanTiltCommand(CameraControlViewModel vm)
    {
        if (vm?.Camera == null) return;

        byte panSpeed = (byte)Math.Clamp((int)_currentPanSpeed, 0, 24);
        byte tiltSpeed = (byte)Math.Clamp((int)_currentTiltSpeed, 0, 20);

        var panDir = _targetPanDir;
        var tiltDir = _targetTiltDir;

        // If speed is near zero for an axis, treat direction as stop
        if (panSpeed == 0) panDir = PanDir.Stop;
        if (tiltSpeed == 0) tiltDir = TiltDir.Stop;

        if (panDir == PanDir.Stop && tiltDir == TiltDir.Stop)
            vm.MoveStop();
        else
            vm.Camera.PanTilt(panSpeed, tiltSpeed, panDir, tiltDir);
    }

    private void ResetJoystickThumb()
    {
        if (_joystickThumb != null)
        {
            Canvas.SetLeft(_joystickThumb, Center - ThumbSize / 2);
            Canvas.SetTop(_joystickThumb, Center - ThumbSize / 2);
        }
    }
}
