using BepInEx.Configuration;
using SilksongBrothers.Network;
using UnityEngine;

namespace SilksongBrothers;

public static class ModConfig
{
    // Visuals
    public static float PlayerOpacity;

    // General
    public static KeyCode MultiplayerToggleKey;
    public static KeyCode SwitchSpectatingPlayerPreviousKey;
    public static KeyCode SwitchSpectatingPlayerNextKey;
    public static float PopupTextDuration;
    public static string StandalonePeerId = Utils.GeneratePeerId();
    public static KeyCode StandaloneServerToggleKey;
    public static string PlayerName = "player";
    public static int ServerHostChangeInterval;

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

        MultiplayerToggleKey = config.Bind("General", "Toggle Key", KeyCode.F5, "Key used to toggle multiplayer.").Value;
        SwitchSpectatingPlayerPreviousKey = config.Bind("General", "Switch Spectating Player Previous Key", KeyCode.LeftArrow, "观战状态下切换前一个观战玩家按键.").Value;
        SwitchSpectatingPlayerNextKey = config.Bind("General", "Switch Spectating Player Next Key", KeyCode.LeftArrow, "观战状态下切换后一个观战玩家按键.").Value;
        PopupTextDuration = config.Bind("General", "Toast Time", 5.0f, "Time until toast messages hide (set this to 0 to disable toast).").Value;
        PlayerName = config.Bind("General", "Player Name", PlayerName, "你的玩家名, 将会在其他玩家游戏中显示.").Value;
        ServerHostChangeInterval = config.Bind("General", "Host Change Interval", 15000, "服务端 host 切换间隔(毫秒).").Value;

        NetworkMode = config.Bind("Network", "Network Mode", NetworkMode.Standalone, "联机网络模式.").Value;
        StandaloneServerToggleKey = config.Bind("Network", "Standalone Server Toggle Key", KeyCode.F6, "独立服务器开关按键.").Value;
        StandaloneServerPort = config.Bind("Network", "Standalone Server Port", Constants.Port, "独立服务器绑定端口.").Value;
        StandaloneServerAddress = config.Bind("Network", "Standalone Server Address", StandaloneServerAddress, "客户端连接的独立服务器地址 (ip:port).").Value;
        StandalonePeerId = config.Bind("Network", "Standalone Peer ID", StandalonePeerId, "独立服务器模式下您的玩家 ID, 用于标识您自己, 如果玩家间重复可能会出现同步问题.").Value;
        RealtimeTimeout = config.Bind("Network", "Realtime Timeout", 1000, "实时包超时时间(ms)").Value;
        // @formatter:on
    }
}
