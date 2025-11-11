using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SilksongBrothers;

#nullable disable

public class MenuButtons : MonoBehaviour
{
    private GameObject _oldButton;
    private GameObject _mainMenu;
    private GameObject _mainMenuContainer;
    private GameObject _mainMenuCommunicatorButton;
    private GameObject _mainMenuStandaloneServerButton;
    private UnityEngine.UI.Text _mainMenuCommunicatorText;
    private UnityEngine.UI.Text _mainMenuStandaloneServerText;

    private void Update()
    {
        try
        {
            if (!_mainMenu)
                _mainMenu = Resources.FindObjectsOfTypeAll<GameObject>()
                    .FirstOrDefault(g => g.name == "MainMenuScreen");
            if (!_mainMenu) return;

            if (!_mainMenuContainer) _mainMenuContainer = _mainMenu.transform.Find("MainMenuButtons").gameObject;
            if (!_mainMenuContainer) return;

            if (!_oldButton) _oldButton = _mainMenuContainer.transform.Find("OptionsButton").gameObject;
            if (!_oldButton) return;

            if (!_mainMenuCommunicatorButton)
            {
                _mainMenuCommunicatorButton = Instantiate(_oldButton, _mainMenuContainer.transform, true);
                _mainMenuCommunicatorButton.transform.localScale = Vector3.one;
                _mainMenuCommunicatorButton.name = "MultiplayerButton";
                _mainMenuCommunicatorText = _mainMenuCommunicatorButton.transform.GetChild(0).gameObject
                    .GetComponent<UnityEngine.UI.Text>();

                var et = _mainMenuCommunicatorButton.GetComponent<EventTrigger>();
                et.triggers.Clear();

                var e = new EventTrigger.Entry();
                e.callback.AddListener(_ => SilksongBrothersPlugin.Instance?.ToggleMultiplayer());
                et.triggers.Add(e);

                Utils.Logger?.LogInfo("Added main menu communicator button.");
            }

            if (!_mainMenuStandaloneServerText)
            {
                _mainMenuStandaloneServerButton = Instantiate(_oldButton, _mainMenuContainer.transform, true);
                _mainMenuStandaloneServerButton.transform.localScale = Vector3.one;
                _mainMenuStandaloneServerButton.name = "StandaloneServerButton";
                _mainMenuStandaloneServerText = _mainMenuStandaloneServerButton.transform.GetChild(0).gameObject
                    .GetComponent<UnityEngine.UI.Text>();

                var et = _mainMenuStandaloneServerButton.GetComponent<EventTrigger>();
                et.triggers.Clear();

                var e = new EventTrigger.Entry();
                e.callback.AddListener(_ => SilksongBrothersPlugin.Instance?.ToggleStandaloneServer());
                et.triggers.Add(e);

                Utils.Logger?.LogInfo("Added main menu standalone server button.");
            }

            _mainMenuCommunicatorText.text = SilksongBrothersPlugin.CommunicatorAlive
                ? $"Disable Multiplayer [{ModConfig.NetworkMode}]"
                : $"Enable Multiplayer [{ModConfig.NetworkMode}]";
            _mainMenuStandaloneServerText.text = SilksongBrothersPlugin.StandaloneServerRunning
                ? "Stop Standalone Server"
                : "Start Standalone Server";
        }
        catch (Exception e)
        {
            Utils.Logger?.LogError(e);
        }
    }
}
