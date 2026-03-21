using PtzJoystickControl.Core.Model;

namespace PtzJoystickControl.Core.Db;

public interface IMappingProfileStore
{
    List<string> GetProfileNames();
    MappingProfile? LoadProfile(string name);
    bool SaveProfile(MappingProfile profile);
    bool DeleteProfile(string name);
}
