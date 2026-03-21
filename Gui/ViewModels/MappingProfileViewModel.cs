using PtzJoystickControl.Core.Db;
using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Model;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PtzJoystickControl.Gui.ViewModels;

public class MappingProfileViewModel : ViewModelBase, INotifyPropertyChanged
{
    private readonly IMappingProfileStore _profileStore;
    private readonly GamepadsViewModel _gamepadsViewModel;
    private string _newProfileName = "";
    private string? _selectedProfile;

    public MappingProfileViewModel(IMappingProfileStore profileStore, GamepadsViewModel gamepadsViewModel)
    {
        _profileStore = profileStore;
        _gamepadsViewModel = gamepadsViewModel;
        RefreshProfiles();
    }

    public ObservableCollection<string> ProfileNames { get; } = new();

    public string? SelectedProfile
    {
        get => _selectedProfile;
        set { _selectedProfile = value; NotifyPropertyChanged(); }
    }

    public string NewProfileName
    {
        get => _newProfileName;
        set { _newProfileName = value; NotifyPropertyChanged(); }
    }

    public void SaveProfile()
    {
        var gamepad = _gamepadsViewModel.SelectedGamepad;
        if (gamepad == null || string.IsNullOrWhiteSpace(_newProfileName)) return;

        var profile = new MappingProfile
        {
            Name = _newProfileName,
            Inputs = gamepad.Inputs.Select(i => new InputSettings(i)).ToList(),
        };

        _profileStore.SaveProfile(profile);
        RefreshProfiles();
        SelectedProfile = _newProfileName;
    }

    public void LoadProfile()
    {
        var gamepad = _gamepadsViewModel.SelectedGamepad;
        if (gamepad == null || string.IsNullOrEmpty(_selectedProfile)) return;

        var profile = _profileStore.LoadProfile(_selectedProfile);
        if (profile?.Inputs == null) return;

        foreach (var inputSetting in profile.Inputs)
        {
            var input = gamepad.Inputs.FirstOrDefault(i => i.Id == inputSetting.Id);
            if (input == null) continue;

            if (inputSetting.CommandType != null)
            {
                var command = input.Commands?.FirstOrDefault(c => c.GetType().ToString() == inputSetting.CommandType);
                input.SelectedCommand = command;
            }
            else
            {
                input.SelectedCommand = null;
            }

            input.Inverted = inputSetting.Inverted;
            input.DeadZone = inputSetting.DeadZoneLow;
            input.Saturation = inputSetting.DeadZoneHigh;

            if (inputSetting.CommandDirection != null)
                input.CommandDirection = inputSetting.CommandDirection;
            if (inputSetting.CommandValue != null)
                input.CommandValue = inputSetting.CommandValue;
        }
    }

    public void DeleteProfile()
    {
        if (string.IsNullOrEmpty(_selectedProfile)) return;
        _profileStore.DeleteProfile(_selectedProfile);
        RefreshProfiles();
        SelectedProfile = ProfileNames.FirstOrDefault();
    }

    private void RefreshProfiles()
    {
        ProfileNames.Clear();
        foreach (var name in _profileStore.GetProfileNames())
            ProfileNames.Add(name);
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
