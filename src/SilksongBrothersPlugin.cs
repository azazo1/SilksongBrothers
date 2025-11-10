using BepInEx;
using HarmonyLib;
using SilksongBrothers.Components;
using UnityEngine;

namespace SilksongBrothers;

[BepInAutoPlugin(id: Constants.ModId, name: Constants.ModName)]
public partial class SilksongBrothersPlugin : BaseUnityPlugin
{
    public static SilksongBrothersPlugin? Instance { get; private set; }
    public Communicator? Communicator;
    public PopupTextManager popupTextManager;

    private void Awake()
    {
        Instance = this;
        Utils.Logger = Logger;
        // Put your initialization logic here
        var version = Utils.Version;
        Logger.LogInfo($"Plugin {Name}:{version} ({Id}) has loaded!");
        ModConfig.Bind(Config);
        new Harmony("io.github.azazo1.silksongbrothers").PatchAll();
        gameObject.AddComponent<MenuButton>();
        // 添加了 canvas 之后才能在屏幕中显示文字.
        var cv = gameObject.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceCamera;
        popupTextManager = gameObject.AddComponent<PopupTextManager>();
    }

    public void ToggleMultiplayer()
    {
        if (Communicator?.Alive != true)
        {
            SpawnPopup("Communicator connecting...");
            Communicator = new Communicator();
        }
        else
        {
            SpawnPopup("Communicator quitting...");
            Communicator.Quit();
            Communicator = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(ModConfig.MultiplayerToggleKey))
        {
            ToggleMultiplayer();
        }

        Communicator?.Update();
        // todo 开关 standalone server
    }

    private void OnDestroy()
    {
        Communicator?.Quit();
        Communicator = null;
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
