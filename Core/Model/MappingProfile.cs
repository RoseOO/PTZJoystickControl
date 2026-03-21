namespace PtzJoystickControl.Core.Model;

public class MappingProfile
{
    public string Name { get; set; } = "Default";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<InputSettings>? Inputs { get; set; }
}
