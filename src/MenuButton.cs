using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SilksongBrothers;

#nullable disable

public class MenuButton : MonoBehaviour
{
    private GameObject _oldButton;
    private GameObject _mainMenu;
    private GameObject _mainMenuContainer;
    private GameObject _mainMenuButton;
    private UnityEngine.UI.Text _mainMenuText;

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

            if (!_mainMenuButton)
            {
                _mainMenuButton = Instantiate(_oldButton, _mainMenuContainer.transform, true);
                _mainMenuButton.transform.localScale = Vector3.one;
                _mainMenuButton.name = "MultiplayerButton";
                _mainMenuText = _mainMenuButton.transform.GetChild(0).gameObject.GetComponent<UnityEngine.UI.Text>();

                EventTrigger et = _mainMenuButton.GetComponent<EventTrigger>();
                et.triggers.Clear();

                EventTrigger.Entry e = new EventTrigger.Entry();
                e.callback.AddListener(_ => SilksongBrothersPlugin.Instance?.ToggleMultiplayer());
                et.triggers.Add(e);

                Utils.Logger?.LogInfo("Added main menu button.");
            }

            _mainMenuText.text = SilksongBrothersPlugin.Instance?.Communicator?.Alive ?? false
                ? $"Disable Multiplayer [{ModConfig.NetworkMode}]"
                : $"Enable Multiplayer [{ModConfig.NetworkMode}]";
        }
        catch (Exception e)
        {
            Utils.Logger?.LogError(e);
        }
    }
}
