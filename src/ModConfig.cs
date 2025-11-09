using BepInEx.Configuration;
using SilksongBrothers.Network;
using UnityEngine;

namespace SilksongBrothers;

public static class ModConfig
{
    // Visuals
    public static float PlayerOpacity;
    public static float CompassOpacity;

    // General
    public static KeyCode MultiplayerToggleKey;
    public static float ToastTime;
    public static string StandalonePeerId = Utils.GeneratePeerId();

    // Audio
    public static bool SyncSound;
    public static bool SyncParticles;
    public static float AudioRolloff;

    // Network
    public static string ServerAddress = $"localhost:{Constants.Port}";
    public static NetworkMode NetworkMode;

    public static void Bind(ConfigFile config)
    {
        PlayerOpacity = config.Bind("Visuals", "Player Opacity", 0.7f, "Opacity of other players (0.0f = invisible, 1.0f = as opaque as yourself).").Value;
        CompassOpacity = config.Bind("Visuals", "Compass Opacity", 0.7f, "Opacity of other players' compasses.").Value;

        MultiplayerToggleKey = config.Bind("General", "Toggle Key", KeyCode.F5, "Key used to toggle multiplayer.").Value;
        ToastTime = config.Bind("General", "Toast Time", 5.0f, "Time until toast messages hide (set this to 0 to disable toast).").Value;

        SyncSound = config.Bind("Audio", "Sync Audio", false, "Enable sound sync (experimental).").Value;
        SyncParticles = config.Bind("Audio", "Sync Particles", false, "Enable particle sync (experimental).").Value;
        AudioRolloff = Mathf.Clamp(config.Bind("Audio", "Audio distance rolloff", 50, "How quickly a sound gets quieter depending on distance").Value, 0, Mathf.Infinity);

        NetworkMode = config.Bind("Network", "Network Mode", NetworkMode.Standalone, "联机网络模式").Value;
        ServerAddress = config.Bind("Network", "Server Address", $"localhost:{Constants.Port}", "服务器地址, 独立服务器模式时才需要指定, 其他模式下值被忽略.").Value;
        StandalonePeerId = config.Bind("General", "Peer ID (Standalone)", StandalonePeerId, "独立服务器模式下的玩家 ID, 用于标识各个玩家, 如果玩家间重复可能会出现同步问题.").Value;
    }
}
