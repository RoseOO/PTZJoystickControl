using PtzJoystickControl.Core.Db;
using PtzJoystickControl.Core.Model;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PtzJoystickControl.Application.Db;

public class MappingProfileStore : IMappingProfileStore
{
    private readonly string _profilesDir;

    public MappingProfileStore()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            _profilesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".PTZJoystickControl/Profiles/");
        else
            _profilesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PTZJoystickControl/Profiles/");

        Directory.CreateDirectory(_profilesDir);
    }

    public List<string> GetProfileNames()
    {
        try
        {
            return Directory.GetFiles(_profilesDir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n)
                .ToList();
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Error listing profiles: {e.Message}");
            return new List<string>();
        }
    }

    public MappingProfile? LoadProfile(string name)
    {
        try
        {
            string filePath = GetFilePath(name);
            if (!File.Exists(filePath)) return null;
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<MappingProfile>(json);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Error loading profile '{name}': {e.Message}");
            return null;
        }
    }

    public bool SaveProfile(MappingProfile profile)
    {
        try
        {
            string filePath = GetFilePath(profile.Name);
            string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            return true;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Error saving profile '{profile.Name}': {e.Message}");
            return false;
        }
    }

    public bool DeleteProfile(string name)
    {
        try
        {
            string filePath = GetFilePath(name);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Error deleting profile '{name}': {e.Message}");
            return false;
        }
    }

    private string GetFilePath(string name)
    {
        // Sanitize the name to prevent path traversal
        string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_profilesDir, safeName + ".json");
    }
}
