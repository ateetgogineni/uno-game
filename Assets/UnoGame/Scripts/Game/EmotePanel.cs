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
    /// <summary>Emote picker that pops up above the emote button.</summary>
    public sealed class EmotePanel : MonoBehaviour
    {
        public RectTransform panel;
        public Button[] emotes = new Button[Emotes.Count];
        public Button closeArea;
        public event Action<int> Picked;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            for (int i = 0; i < emotes.Length; i++)
            {
                int e = i;
                emotes[i].onClick.AddListener(() => { Picked?.Invoke(e); Hide(); });
            }
            closeArea.onClick.AddListener(Hide);
        }

        public void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            panel.localScale = Vector3.one * 0.3f;
            Tw.Scale(panel, 1f, 0.3f, Ease.OutBack);
        }

        public void Hide()
        {
            if (!IsOpen) return;
            Tw.Scale(panel, 0.3f, 0.12f, Ease.InQuad).OnComplete(() => gameObject.SetActive(false));
        }
    }
}
