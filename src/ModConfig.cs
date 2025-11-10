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
    public static float PopupTextDuration;
    public static string StandalonePeerId = Utils.GeneratePeerId();
    public static KeyCode StandaloneServerToggleKey;
    public static string PlayerName = "player";

    // Audio
    public static bool SyncSound;
    public static bool SyncParticles;
    public static float AudioRolloff;

    // Network
    public static string StandaloneServerAddress = $"localhost:{Constants.Port}";
    public static NetworkMode NetworkMode;
    public static ushort StandaloneServerPort;
    public static long RealtimeTimeout;

    public static void Bind(ConfigFile config)
    {
        // @formatter:off
        // _ = config.Bind("Notice", "Restart to Take Effect", "", "以下设置可能需要重启游戏才能生效.").Value;
        // todo 监听 setting changes
        PlayerOpacity = config.Bind("Visuals", "Player Opacity", 0.7f, "Opacity of other players (0.0f = invisible, 1.0f = as opaque as yourself).").Value;
        CompassOpacity = config.Bind("Visuals", "Compass Opacity", 0.7f, "Opacity of other players' compasses.").Value;

        MultiplayerToggleKey = config.Bind("General", "Toggle Key", KeyCode.F5, "Key used to toggle multiplayer.").Value;
        PopupTextDuration = config.Bind("General", "Toast Time", 5.0f, "Time until toast messages hide (set this to 0 to disable toast).").Value;
        PlayerName = config.Bind("General", "Player Name", PlayerName, "你的玩家名, 将会在其他玩家游戏中显示.").Value;

        SyncSound = config.Bind("Audio", "Sync Audio", false, "Enable sound sync (experimental).").Value;
        SyncParticles = config.Bind("Audio", "Sync Particles", false, "Enable particle sync (experimental).").Value;
        AudioRolloff = Mathf.Clamp(config.Bind("Audio", "Audio distance rolloff", 50, "How quickly a sound gets quieter depending on distance").Value, 0, Mathf.Infinity);

        NetworkMode = config.Bind("Network", "Network Mode", NetworkMode.Standalone, "联机网络模式.").Value;
        StandaloneServerToggleKey = config.Bind("Network", "Standalone Server Toggle Key", KeyCode.F6, "独立服务器开关按键.").Value;
        StandaloneServerPort = config.Bind("Network", "Standalone Server Port", Constants.Port, "独立服务器绑定端口.").Value;
        StandaloneServerAddress = config.Bind("Network", "Standalone Server Address", StandaloneServerAddress, "客户端连接的独立服务器地址 (ip:port).").Value;
        StandalonePeerId = config.Bind("Network", "Standalone Peer ID", StandalonePeerId, "独立服务器模式下您的玩家 ID, 用于标识您自己, 如果玩家间重复可能会出现同步问题.").Value;
        RealtimeTimeout = config.Bind("Network", "Realtime Timeout", 1000, "实时包超时时间(ms)").Value;
        // @formatter:on
    }
}
