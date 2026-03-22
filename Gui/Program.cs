using Avalonia;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using PtzJoystickControl.Gui.ViewModels;
using PtzJoystickControl.Gui.TrayIcon;
using PtzJoystickControl.Application.Db;
using PtzJoystickControl.Application.Services;
using PtzJoystickControl.Core.Db;
using PtzJoystickControl.Core.Services;
using PtzJoystickControl.SdlGamepads.Services;
using PtzJoystickControl.KeyboardInput.Services;
using PtzJoystickControl.MidiInput.Services;
using PtzJoystickControl.OscInput.Services;
using PtzJoystickControl.WebInterface.Services;
using Splat;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Octokit;
using System.Reflection;

namespace PtzJoystickControl.Gui;

internal class Program
{
    //private static FileStream? f;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        string logDirectory = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".PTZJoystickControl/")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PTZJoystickControl/");

        Directory.CreateDirectory(logDirectory);

        // Enable logging to file for both Debug and Release builds
        string logFilePath = Path.Combine(logDirectory, "log.txt");
        var fileListener = new TextWriterTraceListener(logFilePath)
        {
            TraceOutputOptions = TraceOptions.DateTime | TraceOptions.ProcessId
        };
        Trace.Listeners.Add(fileListener);
        Trace.AutoFlush = true;
        Debug.AutoFlush = true;

        var appBuilder = BuildAvaloniaApp();

        // Mutex to ensure only one instance will 
        var mutex = new Mutex(false, "PTZJoystickControlMutex/BFD0A32E-F433-49E7-AB74-B49FC95012D0");
        try
        {
            if (!mutex.WaitOne(0, false))
            {
                appBuilder.StartWithClassicDesktopLifetime(new string[] { "-r" }, Avalonia.Controls.ShutdownMode.OnMainWindowClose);
                return;
            }

            RegisterServices();

            appBuilder.StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
        }
        finally
        {
            mutex?.Close();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();

    private static void RegisterServices()
    {
        var services = Locator.CurrentMutable;
        var resolver = Locator.Current;
        var avaloniaLocator = AvaloniaLocator.Current;

        services.Register<IGitHubClient>(() => new GitHubClient(new ProductHeaderValue("PTZJoystickControl-UpdateChecker")));
        services.Register<IUpdateService>(() => new UpdateService(
            resolver.GetServiceOrThrow<IGitHubClient>(),
            "RoseOO",
            "PTZJoystickControl",
            Assembly.GetExecutingAssembly().GetName().Version!));

        services.RegisterLazySingleton<ICameraSettingsStore>(() => new CameraSettingsStore());
        services.RegisterLazySingleton<IGamepadSettingsStore>(() => new GamepadSettingsStore());
        services.RegisterLazySingleton<IMappingProfileStore>(() => new MappingProfileStore());

        services.RegisterLazySingleton<IVmixService>(() => new VmixService());
        services.RegisterLazySingleton<ICommandsService>(() => new CommandsService(
            resolver.GetServiceOrThrow<IVmixService>()));
        services.RegisterLazySingleton<ICamerasService>(() => new CamerasService(
            resolver.GetServiceOrThrow<ICameraSettingsStore>()));

        // Register individual input device services
        var gamepadSettingsStore = resolver.GetServiceOrThrow<IGamepadSettingsStore>();
        var camerasService = resolver.GetServiceOrThrow<ICamerasService>();
        var commandsService = resolver.GetServiceOrThrow<ICommandsService>();

        var sdlService = new SdlGamepadsService(gamepadSettingsStore, camerasService, commandsService);
        var keyboardService = new KeyboardGamepadsService(gamepadSettingsStore, camerasService, commandsService);
        var midiService = new MidiGamepadsService(gamepadSettingsStore, camerasService, commandsService);
        var oscService = new OscGamepadsService(gamepadSettingsStore, camerasService, commandsService);

        // Register the keyboard service separately so it can receive key events
        services.RegisterLazySingleton(() => keyboardService);

        // Register the composite service that aggregates all input device types
        services.RegisterLazySingleton<IGamepadsService>(() => new CompositeGamepadsService(
            new IGamepadsService[] { sdlService, keyboardService, midiService, oscService }));

        // Register the web interface service
        services.RegisterLazySingleton(() => new WebInterfaceService(
            resolver.GetServiceOrThrow<ICamerasService>(),
            resolver.GetServiceOrThrow<IGamepadsService>()));

        services.RegisterLazySingleton(() => new GamepadsViewModel(
            resolver.GetServiceOrThrow<IGamepadsService>(),
            resolver.GetServiceOrThrow<IMappingProfileStore>(),
            resolver.GetServiceOrThrow<ICamerasService>()));
        services.Register(() => new CamerasViewModel(
            resolver.GetServiceOrThrow<ICamerasService>(),
            resolver.GetServiceOrThrow<GamepadsViewModel>(),
            resolver.GetServiceOrThrow<IVmixService>()));
        services.RegisterLazySingleton(() => new CameraControlViewModel());
        services.RegisterLazySingleton(() => new VmixViewModel(
            resolver.GetServiceOrThrow<IVmixService>()));
        services.RegisterLazySingleton(() => new TrayIconHandler(
            avaloniaLocator.GetServiceOrThrow<IAssetLoader>()));
    }
}

internal static class ResolverExtension
{
    internal static T GetServiceOrThrow<T>(this IReadonlyDependencyResolver resolver)
    {
        return resolver.GetService<T>()
            ?? throw new Exception("Resolved dependency cannot be null");
    }

    internal static T GetServiceOrThrow<T>(this IAvaloniaDependencyResolver resolver)
    {
        return resolver.GetService<T>()
            ?? throw new Exception("Resolved dependency cannot be null");
    }
}
