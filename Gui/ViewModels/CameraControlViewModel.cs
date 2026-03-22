using PtzJoystickControl.Core.Devices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.Gui.ViewModels;

public class CameraControlViewModel : ViewModelBase, INotifyPropertyChanged
{
    private ViscaDeviceBase? _camera;
    private byte _panSpeed = 12;
    private byte _tiltSpeed = 10;
    private byte _zoomSpeed = 4;
    private byte _focusSpeed = 4;
    private bool _joystickRampingEnabled;
    private float _joystickRampTime = 0.3f;

    public ViscaDeviceBase? Camera
    {
        get => _camera;
        set
        {
            if (_camera != value)
            {
                if (_camera != null)
                {
                    _camera.PropertyChanged -= OnCameraPropertyChanged;
                    _camera.PollingEnabled = false;
                }
                _camera = value;
                if (_camera != null)
                {
                    _camera.PropertyChanged += OnCameraPropertyChanged;
                    _camera.PollingEnabled = true;
                }
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(HasCamera));
                NotifyPropertyChanged(nameof(PollingEnabled));
                NotifyFeedbackProperties();
            }
        }
    }

    private void OnCameraPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ViscaDeviceBase.ZoomPosition):
            case nameof(ViscaDeviceBase.PanPosition):
            case nameof(ViscaDeviceBase.TiltPosition):
            case nameof(ViscaDeviceBase.FocusPosition):
            case nameof(ViscaDeviceBase.FocusModeState):
            case nameof(ViscaDeviceBase.ExposureModeState):
            case nameof(ViscaDeviceBase.IrisPosition):
            case nameof(ViscaDeviceBase.ShutterPosition):
            case nameof(ViscaDeviceBase.GainPosition):
            case nameof(ViscaDeviceBase.WhiteBalanceModeState):
            case nameof(ViscaDeviceBase.RGainPosition):
            case nameof(ViscaDeviceBase.BGainPosition):
            case nameof(ViscaDeviceBase.AperturePosition):
            case nameof(ViscaDeviceBase.BacklightState):
            case nameof(ViscaDeviceBase.PowerState):
            case nameof(ViscaDeviceBase.Connected):
            case nameof(ViscaDeviceBase.PollingEnabled):
                NotifyPropertyChanged(e.PropertyName!);
                break;
        }
    }

    private void NotifyFeedbackProperties()
    {
        NotifyPropertyChanged(nameof(ZoomPosition));
        NotifyPropertyChanged(nameof(PanPosition));
        NotifyPropertyChanged(nameof(TiltPosition));
        NotifyPropertyChanged(nameof(FocusPosition));
        NotifyPropertyChanged(nameof(FocusModeState));
        NotifyPropertyChanged(nameof(ExposureModeState));
        NotifyPropertyChanged(nameof(IrisPosition));
        NotifyPropertyChanged(nameof(ShutterPosition));
        NotifyPropertyChanged(nameof(GainPosition));
        NotifyPropertyChanged(nameof(WhiteBalanceModeState));
        NotifyPropertyChanged(nameof(RGainPosition));
        NotifyPropertyChanged(nameof(BGainPosition));
        NotifyPropertyChanged(nameof(AperturePosition));
        NotifyPropertyChanged(nameof(BacklightState));
        NotifyPropertyChanged(nameof(PowerState));
        NotifyPropertyChanged(nameof(Connected));
        NotifyPropertyChanged(nameof(PollingEnabled));
    }

    // Camera feedback properties (read-through to camera)
    public ushort? ZoomPosition => _camera?.ZoomPosition;
    public short? PanPosition => _camera?.PanPosition;
    public short? TiltPosition => _camera?.TiltPosition;
    public ushort? FocusPosition => _camera?.FocusPosition;
    public FocusMode? FocusModeState => _camera?.FocusModeState;
    public ExposureMode? ExposureModeState => _camera?.ExposureModeState;
    public ushort? IrisPosition => _camera?.IrisPosition;
    public ushort? ShutterPosition => _camera?.ShutterPosition;
    public ushort? GainPosition => _camera?.GainPosition;
    public WhiteBalanceMode? WhiteBalanceModeState => _camera?.WhiteBalanceModeState;
    public ushort? RGainPosition => _camera?.RGainPosition;
    public ushort? BGainPosition => _camera?.BGainPosition;
    public ushort? AperturePosition => _camera?.AperturePosition;
    public BacklightCompensation? BacklightState => _camera?.BacklightState;
    public Power? PowerState => _camera?.PowerState;
    public bool Connected => _camera?.Connected ?? false;

    public bool PollingEnabled
    {
        get => _camera?.PollingEnabled ?? false;
        set { if (_camera != null) _camera.PollingEnabled = value; NotifyPropertyChanged(); }
    }

    public bool HasCamera => _camera != null;

    public byte PanSpeed { get => _panSpeed; set { _panSpeed = Math.Clamp(value, (byte)1, (byte)24); NotifyPropertyChanged(); } }
    public byte TiltSpeed { get => _tiltSpeed; set { _tiltSpeed = Math.Clamp(value, (byte)1, (byte)20); NotifyPropertyChanged(); } }
    public byte ZoomSpeed { get => _zoomSpeed; set { _zoomSpeed = Math.Clamp(value, (byte)0, (byte)7); NotifyPropertyChanged(); } }
    public byte FocusSpeed { get => _focusSpeed; set { _focusSpeed = Math.Clamp(value, (byte)0, (byte)7); NotifyPropertyChanged(); } }

    public bool JoystickRampingEnabled
    {
        get => _joystickRampingEnabled;
        set { _joystickRampingEnabled = value; NotifyPropertyChanged(); }
    }

    public float JoystickRampTime
    {
        get => _joystickRampTime;
        set { _joystickRampTime = Math.Clamp(value, 0.05f, 2.0f); NotifyPropertyChanged(); }
    }

    // PTZ Movement
    public void PanLeft() => _camera?.Pan(_panSpeed, PanDir.Left);
    public void PanRight() => _camera?.Pan(_panSpeed, PanDir.Right);
    public void PanStop() => _camera?.Pan(0, PanDir.Stop);
    public void TiltUp() => _camera?.Tilt(_tiltSpeed, TiltDir.Up);
    public void TiltDown() => _camera?.Tilt(_tiltSpeed, TiltDir.Down);
    public void TiltStop() => _camera?.Tilt(0, TiltDir.Stop);
    public void MoveUpLeft() => _camera?.PanTilt(_panSpeed, _tiltSpeed, PanDir.Left, TiltDir.Up);
    public void MoveUpRight() => _camera?.PanTilt(_panSpeed, _tiltSpeed, PanDir.Right, TiltDir.Up);
    public void MoveDownLeft() => _camera?.PanTilt(_panSpeed, _tiltSpeed, PanDir.Left, TiltDir.Down);
    public void MoveDownRight() => _camera?.PanTilt(_panSpeed, _tiltSpeed, PanDir.Right, TiltDir.Down);
    public void MoveStop() => _camera?.PanTilt(0, 0, PanDir.Stop, TiltDir.Stop);

    // Zoom
    public void ZoomIn() => _camera?.Zoom(_zoomSpeed, ZoomDir.Tele);
    public void ZoomOut() => _camera?.Zoom(_zoomSpeed, ZoomDir.Wide);
    public void ZoomStop() => _camera?.Zoom(0, ZoomDir.Stop);

    // Focus
    public void FocusFar() => _camera?.Focus(_focusSpeed, FocusDir.Far);
    public void FocusNear() => _camera?.Focus(_focusSpeed, FocusDir.Near);
    public void FocusStop() => _camera?.Focus(0, FocusDir.Stop);
    public void FocusAuto() => _camera?.FocusMode(FocusMode.Auto);
    public void FocusManual() => _camera?.FocusMode(FocusMode.Manual);
    public void FocusToggle() => _camera?.FocusMode(FocusMode.Toggle);

    // Power
    public void PowerOn() => _camera?.Power(Power.On);
    public void PowerOff() => _camera?.Power(Power.Off);

    // Presets
    public void RecallPreset(object? param)
    {
        if (param != null && byte.TryParse(param.ToString(), out byte number))
            _camera?.Preset(Preset.Recall, number);
    }
    public void SetPreset(object? param)
    {
        if (param != null && byte.TryParse(param.ToString(), out byte number))
            _camera?.Preset(Preset.Set, number);
    }

    // Exposure
    public static IEnumerable<ExposureMode> ExposureModes => Enum.GetValues<ExposureMode>();
    private ExposureMode _selectedExposureMode;
    public ExposureMode SelectedExposureMode
    {
        get => _selectedExposureMode;
        set { _selectedExposureMode = value; NotifyPropertyChanged(); _camera?.SetExposureMode(value); }
    }
    public void SetExposureMode(ExposureMode mode) => _camera?.SetExposureMode(mode);
    public void IrisUp() => _camera?.AdjustIris(IrisDir.Up);
    public void IrisDown() => _camera?.AdjustIris(IrisDir.Down);
    public void IrisReset() => _camera?.AdjustIris(IrisDir.Reset);
    public void ShutterUp() => _camera?.AdjustShutter(ShutterDir.Up);
    public void ShutterDown() => _camera?.AdjustShutter(ShutterDir.Down);
    public void ShutterReset() => _camera?.AdjustShutter(ShutterDir.Reset);
    public void GainUp() => _camera?.AdjustGain(GainDir.Up);
    public void GainDown() => _camera?.AdjustGain(GainDir.Down);
    public void GainReset() => _camera?.AdjustGain(GainDir.Reset);

    // White Balance
    public static IEnumerable<WhiteBalanceMode> WhiteBalanceModes => Enum.GetValues<WhiteBalanceMode>();
    private WhiteBalanceMode _selectedWhiteBalanceMode;
    public WhiteBalanceMode SelectedWhiteBalanceMode
    {
        get => _selectedWhiteBalanceMode;
        set { _selectedWhiteBalanceMode = value; NotifyPropertyChanged(); _camera?.SetWhiteBalanceMode(value); }
    }
    public void SetWhiteBalanceMode(WhiteBalanceMode mode) => _camera?.SetWhiteBalanceMode(mode);
    public void WbTrigger() => _camera?.TriggerWhiteBalance();
    public void RedGainUp() => _camera?.AdjustRedGain(GainDir.Up);
    public void RedGainDown() => _camera?.AdjustRedGain(GainDir.Down);
    public void RedGainReset() => _camera?.AdjustRedGain(GainDir.Reset);
    public void BlueGainUp() => _camera?.AdjustBlueGain(GainDir.Up);
    public void BlueGainDown() => _camera?.AdjustBlueGain(GainDir.Down);
    public void BlueGainReset() => _camera?.AdjustBlueGain(GainDir.Reset);

    // Backlight & Aperture
    public void BacklightOn() => _camera?.SetBacklightCompensation(BacklightCompensation.On);
    public void BacklightOff() => _camera?.SetBacklightCompensation(BacklightCompensation.Off);
    public void ApertureUp() => _camera?.AdjustAperture(ApertureDir.Up);
    public void ApertureDown() => _camera?.AdjustAperture(ApertureDir.Down);
    public void ApertureReset() => _camera?.AdjustAperture(ApertureDir.Reset);

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
