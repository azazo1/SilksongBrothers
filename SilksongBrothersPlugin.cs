using System;
using BepInEx;
using HarmonyLib;
using SilksongBrothers.Network;
using Unity.TLS.LowLevel;

namespace SilksongBrothers;

[BepInAutoPlugin(id: "io.github.azazo1.silksongbrothers", "SilksongBrothers")]
public partial class SilksongBrothersPlugin : BaseUnityPlugin
{
    private Communicator _communicator;

    private void Awake()
    {
        Utils.Logger = Logger;
        // Put your initialization logic here
        var version = Utils.Version;
        Logger.LogInfo($"Plugin {Name}:{version} ({Id}) has loaded!");
        ModConfig.Bind(Config);
        new Harmony("io.github.azazo1.silksongbrothers").PatchAll();

        _communicator = new Communicator();
    }

    private void OnDestroy()
    {
        _communicator.Quit();
        Logger.LogInfo($"Plugin {Name} has been destroyed!");
        Utils.Logger = null;
    }
}
