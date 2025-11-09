using BepInEx;

namespace SilksongBrothers;

[BepInAutoPlugin(id: "io.github.azazo1.silksongbrothers", "SilksongBrothers")]
public partial class SilksongBrothersPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        // Put your initialization logic here
        var version = Utils.Version;
        Logger.LogInfo($"Plugin {Name}:{version} ({Id}) has loaded!");
        ModConfig.Bind(Config);

    }
}
