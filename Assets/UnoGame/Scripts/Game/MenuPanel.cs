using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnoGame.Core;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Game
{
    /// <summary>Small menu under the "..." button.</summary>
    public sealed class MenuPanel : MonoBehaviour
    {
        public RectTransform panel;
        public Button soundButton;
        public Image soundIcon;
        public TextMeshProUGUI soundLabel;
        public Button leaveButton;
        public Button closeArea;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            closeArea.onClick.AddListener(Hide);
            soundButton.onClick.AddListener(() =>
            {
                App.I.SetSound(!App.I.Profile.sound);
                Refresh();
            });
        }

        void Refresh()
        {
            bool on = App.I.Profile.sound;
            soundIcon.sprite = UIKit.S(on ? "icon_sound_on" : "icon_sound_off");
            soundLabel.text = on ? "Sound: On" : "Sound: Off";
        }

        public void Show()
        {
            Refresh();
            gameObject.SetActive(true);
            panel.localScale = new Vector3(1f, 0.2f, 1f);
            Tw.Scale(panel, 1f, 0.25f, Ease.OutBack);
        }

        public void Hide()
        {
            if (IsOpen) gameObject.SetActive(false);
        }
    }
}
