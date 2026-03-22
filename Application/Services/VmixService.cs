using PtzJoystickControl.Core.Model;
using PtzJoystickControl.Core.Services;
using System.Diagnostics;
using System.Net.Http;
using System.Xml.Linq;

namespace PtzJoystickControl.Application.Services;

public class VmixService : IVmixService, IDisposable
{
    private HttpClient? _httpClient;
    private readonly object _lock = new();
    private VmixSettings _settings;
    private Timer? _pollTimer;
    private int _lastPreviewInput;
    private volatile bool _polling;

    public VmixService()
    {
        _settings = LoadSettings();
        Host = _settings.Host;
        Port = _settings.Port;
        AutoPreview = _settings.AutoPreview;
        AutoCameraSelect = _settings.AutoCameraSelect;
    }

    public bool IsConnected { get; private set; }
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8088;
    public bool AutoPreview { get; set; } = true;

    private bool _autoCameraSelect;
    public bool AutoCameraSelect
    {
        get => _autoCameraSelect;
        set
        {
            _autoCameraSelect = value;
            if (value && IsConnected)
                StartPolling();
            else
                StopPolling();
        }
    }

    public event Action<int>? PreviewInputChanged;

    public Dictionary<int, int> CameraToVmixInput
    {
        get => _settings.CameraToVmixInput;
        set => _settings.CameraToVmixInput = value;
    }

    public async Task ConnectAsync()
    {
        try
        {
            _httpClient?.Dispose();
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri($"http://{Host}:{Port}/"),
                Timeout = TimeSpan.FromSeconds(5)
            };

            var response = await _httpClient.GetAsync("api");
            IsConnected = response.IsSuccessStatusCode;

            if (IsConnected && AutoCameraSelect)
                StartPolling();
        }
        catch (Exception e)
        {
            Debug.WriteLine($"vMix connect error: {e.Message}");
            IsConnected = false;
        }
    }

    public void Disconnect()
    {
        StopPolling();
        _httpClient?.Dispose();
        _httpClient = null;
        IsConnected = false;
    }

    public async Task SendPreviewInputAsync(int inputNumber)
    {
        await SendFunctionAsync($"PreviewInput&Input={inputNumber}");
    }

    public async Task SendCutAsync(int? mix = null)
    {
        var function = "Cut";
        if (mix.HasValue)
            function += $"&Mix={mix.Value}";
        await SendFunctionAsync(function);
    }

    public async Task SendFadeAsync(int duration, int? mix = null)
    {
        var function = $"Fade&Duration={duration}";
        if (mix.HasValue)
            function += $"&Mix={mix.Value}";
        await SendFunctionAsync(function);
    }

    private async Task SendFunctionAsync(string function)
    {
        if (_httpClient == null || !IsConnected)
            return;

        try
        {
            var response = await _httpClient.GetAsync($"api?Function={function}");
            if (!response.IsSuccessStatusCode)
                Debug.WriteLine($"vMix API error: {response.StatusCode}");
        }
        catch (Exception e)
        {
            Debug.WriteLine($"vMix send error: {e.Message}");
            IsConnected = false;
        }
    }

    public void SaveSettings()
    {
        _settings.Host = Host;
        _settings.Port = Port;
        _settings.Enabled = IsConnected;
        _settings.AutoPreview = AutoPreview;
        _settings.AutoCameraSelect = AutoCameraSelect;

        try
        {
            string configDir = GetConfigDir();
            string filePath = Path.Combine(configDir, "VmixSettings.json");
            var json = System.Text.Json.JsonSerializer.Serialize(_settings);
            File.WriteAllText(filePath, json);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"vMix save settings error: {e.Message}");
        }
    }

    private VmixSettings LoadSettings()
    {
        try
        {
            string configDir = GetConfigDir();
            string filePath = Path.Combine(configDir, "VmixSettings.json");
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return System.Text.Json.JsonSerializer.Deserialize<VmixSettings>(json) ?? new VmixSettings();
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"vMix load settings error: {e.Message}");
        }
        return new VmixSettings();
    }

    private static string GetConfigDir()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".PTZJoystickControl/");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PTZJoystickControl/");
    }

    private void StartPolling()
    {
        _pollTimer?.Dispose();
        _pollTimer = new Timer(PollPreviewInput, null, 100, 100);
    }

    private void StopPolling()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private async void PollPreviewInput(object? state)
    {
        if (_httpClient == null || !IsConnected || !AutoCameraSelect)
            return;

        if (_polling)
            return;
        _polling = true;

        try
        {
            var response = await _httpClient.GetAsync("api");
            if (!response.IsSuccessStatusCode)
                return;

            var content = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(content);
            var previewElement = doc.Root?.Element("preview");
            if (previewElement != null && int.TryParse(previewElement.Value, out int previewInput))
            {
                if (previewInput != _lastPreviewInput)
                {
                    _lastPreviewInput = previewInput;
                    PreviewInputChanged?.Invoke(previewInput);
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"vMix poll error: {e.Message}");
        }
        finally
        {
            _polling = false;
        }
    }

    public void Dispose()
    {
        StopPolling();
        _httpClient?.Dispose();
    }
}
