using PtzJoystickControl.Core.Devices;
using PtzJoystickControl.Core.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace PtzJoystickControl.Application.Services;

public class CompositeGamepadsService : IGamepadsService
{
    private readonly IReadOnlyList<IGamepadsService> _services;

    public ObservableCollection<IGamepadInfo> Gamepads { get; } = new();
    public ObservableCollection<IGamepad> ActiveGamepads { get; } = new();

    public CompositeGamepadsService(IEnumerable<IGamepadsService> services)
    {
        _services = services.ToList();

        foreach (var service in _services)
        {
            foreach (var gamepad in service.Gamepads)
                Gamepads.Add(gamepad);

            foreach (var active in service.ActiveGamepads)
                ActiveGamepads.Add(active);

            service.Gamepads.CollectionChanged += OnGamepadsCollectionChanged;
            service.ActiveGamepads.CollectionChanged += OnActiveGamepadsCollectionChanged;
        }
    }

    public void ActivateGamepad(IGamepadInfo gamepad)
    {
        foreach (var service in _services)
        {
            if (service.Gamepads.Contains(gamepad))
            {
                service.ActivateGamepad(gamepad);
                return;
            }
        }
    }

    public void DeactivateGamepad(IGamepadInfo gamepad)
    {
        foreach (var service in _services)
        {
            if (service.Gamepads.Contains(gamepad))
            {
                service.DeactivateGamepad(gamepad);
                return;
            }
        }
    }

    private void OnGamepadsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (IGamepadInfo item in e.NewItems!)
                    Gamepads.Add(item);
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (IGamepadInfo item in e.OldItems!)
                    Gamepads.Remove(item);
                break;

            case NotifyCollectionChangedAction.Replace:
                foreach (IGamepadInfo item in e.OldItems!)
                    Gamepads.Remove(item);
                foreach (IGamepadInfo item in e.NewItems!)
                    Gamepads.Add(item);
                break;

            case NotifyCollectionChangedAction.Reset:
                RebuildCollection(Gamepads, s => s.Gamepads);
                break;
        }
    }

    private void OnActiveGamepadsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (IGamepad item in e.NewItems!)
                    ActiveGamepads.Add(item);
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (IGamepad item in e.OldItems!)
                    ActiveGamepads.Remove(item);
                break;

            case NotifyCollectionChangedAction.Replace:
                foreach (IGamepad item in e.OldItems!)
                    ActiveGamepads.Remove(item);
                foreach (IGamepad item in e.NewItems!)
                    ActiveGamepads.Add(item);
                break;

            case NotifyCollectionChangedAction.Reset:
                RebuildCollection(ActiveGamepads, s => s.ActiveGamepads);
                break;
        }
    }

    private void RebuildCollection<T>(ObservableCollection<T> target, Func<IGamepadsService, IEnumerable<T>> selector)
    {
        target.Clear();
        foreach (var service in _services)
        {
            foreach (var item in selector(service))
                target.Add(item);
        }
    }
}
