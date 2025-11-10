using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SilksongBrothers.Components;

#nullable disable

public class PopupTextManager : MonoBehaviour
{
    private class PopupEntry
    {
        public GameObject GameObject;
        public RectTransform RectTransform;
        public float Time;
    }

    private GameObject _text;
    private readonly Queue<PopupEntry> _popups = new();

    private void Update()
    {
        if (!_text)
            _text = GameObject.Find(
                "_UIManager/UICanvas/MainMenuScreen/MainMenuButtons/OptionsButton/Menu Button Text");

        while (_popups.Count > 0 && ModConfig.PopupTextDuration < Time.unscaledTime - _popups.Peek().Time)
        {
            var e = _popups.Dequeue();
            Destroy(e.GameObject);
        }
    }

    public void SpawnPopup(string text, Color color = default)
    {
        try
        {
            if (!_text) return;
            if (ModConfig.PopupTextDuration == 0) return;

            var newObject = new GameObject();
            newObject.transform.SetParent(transform);
            newObject.name = $"SilksongBrothers - {text}";

            var newTransform = newObject.AddComponent<RectTransform>();
            newTransform.sizeDelta = new Vector2(600, 50);
            newTransform.anchorMin = Vector3.zero;
            newTransform.anchorMax = Vector3.zero;
            newTransform.anchoredPosition = new Vector2(40 + newTransform.sizeDelta.x / 2, 80);

            var newText = newObject.AddComponent<Text>();
            newText.font = _text.GetComponent<Text>().font;
            newText.fontSize = 14;
            newText.alignment = TextAnchor.MiddleLeft;
            newText.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (color != default) newText.color = color;
            newText.text = text;

            foreach (var e2 in _popups) e2.RectTransform.anchoredPosition += new Vector2(0, 25);

            _popups.Enqueue(new PopupEntry
            {
                GameObject = newObject,
                RectTransform = newTransform,
                Time = Time.unscaledTime,
            });
        }
        catch (Exception e)
        {
            Utils.Logger?.LogError(e);
        }
    }
}
