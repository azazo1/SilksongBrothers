using BepInEx;

namespace SilksongBrothers;

// TODO - adjust the plugin guid as needed
[BepInAutoPlugin(id: "io.github.azazo1.silksongbrothers")]
public partial class SilksongBrothersPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        // Put your initialization logic here
        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
    }
}
