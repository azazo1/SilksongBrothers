using BepInEx;
using HarmonyLib;
using SilksongBrothers.Components;
using SilksongBrothers.Network.Standalone;
using UnityEngine;

namespace SilksongBrothers;

[BepInAutoPlugin(id: Constants.ModId, name: Constants.ModName)]
public partial class SilksongBrothersPlugin : BaseUnityPlugin
{
    public static SilksongBrothersPlugin? Instance { get; private set; }
    private Communicator? _communicator;
    public PopupTextManager popupTextManager;
    private readonly StandaloneServer _server = new();

    public static bool CommunicatorAlive
    {
        get
        {
            if (!Instance) return false;
            return Instance._communicator?.Alive ?? false;
        }
    }

    public static bool StandaloneServerRunning => Instance && Instance._server.Running;

    private void Awake()
    {
        Instance = this;
        Utils.Logger = Logger;
        // Put your initialization logic here
        var version = Utils.Version;
        Logger.LogInfo($"Plugin {Name}:{version} ({Id}) has loaded!");
        ModConfig.Bind(Config);
        new Harmony("io.github.azazo1.silksongbrothers").PatchAll();
        gameObject.AddComponent<MenuButtons>();
        // 添加了 canvas 之后才能在屏幕中显示文字.
        var cv = gameObject.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceCamera;
        popupTextManager = gameObject.AddComponent<PopupTextManager>();

        _server.OnServerCrashed += e =>
        {
            SpawnPopup($"Standalone server crashed: {e.Message}", Color.red);
            Utils.Logger?.LogError(e);
        };
    }

    public void ToggleMultiplayer()
    {
        if (_communicator?.Alive != true)
        {
            SpawnPopup("Communicator connecting...");
            _communicator = new Communicator();
        }
        else
        {
            SpawnPopup("Communicator quitting...");
            _communicator?.Quit();
            _communicator = null;
        }
    }

    public void ToggleStandaloneServer()
    {
        if (_server.Running)
        {
            _server.Stop().ContinueWith(_ => { SpawnPopup("Standalone server stopped."); });
        }
        else
        {
            _server.Start();
            SpawnPopup("Standalone server started.");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(ModConfig.MultiplayerToggleKey))
        {
            ToggleMultiplayer();
        }

        if (Input.GetKeyDown(ModConfig.StandaloneServerToggleKey))
        {
            ToggleStandaloneServer();
        }

        _communicator?.Update();
    }

    private void OnDestroy()
    {
        _communicator?.Quit();
        _communicator = null;
        Logger.LogInfo($"Plugin {Name} has been destroyed!");
        Utils.Logger = null;
        Instance = null;
    }

    public static void SpawnPopup(string text, Color color = default)
    {
        if (!Instance) return;
        if (!Instance.popupTextManager) return;
        Utils.Logger?.LogInfo($"Spawn popup: {text.TrimEnd()}");
        Instance.popupTextManager.SpawnPopup(text, color);
    }
}
