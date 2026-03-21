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

    public ViscaDeviceBase? Camera
    {
        get => _camera;
        set
        {
            if (_camera != value)
            {
                _camera = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(HasCamera));
            }
        }
    }

    public bool HasCamera => _camera != null;

    public byte PanSpeed { get => _panSpeed; set { _panSpeed = Math.Clamp(value, (byte)1, (byte)24); NotifyPropertyChanged(); } }
    public byte TiltSpeed { get => _tiltSpeed; set { _tiltSpeed = Math.Clamp(value, (byte)1, (byte)20); NotifyPropertyChanged(); } }
    public byte ZoomSpeed { get => _zoomSpeed; set { _zoomSpeed = Math.Clamp(value, (byte)0, (byte)7); NotifyPropertyChanged(); } }
    public byte FocusSpeed { get => _focusSpeed; set { _focusSpeed = Math.Clamp(value, (byte)0, (byte)7); NotifyPropertyChanged(); } }

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
