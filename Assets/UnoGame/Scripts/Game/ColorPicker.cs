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
    /// <summary>"Choose a color" dialog shown when a wild card is played.</summary>
    public sealed class ColorPicker : MonoBehaviour
    {
        public CanvasGroup group;
        public RectTransform panel;
        public Button[] balls = new Button[4];
        public Button dim;
        Action<int> callback;
        bool closing;

        public bool IsOpen => gameObject.activeSelf && !closing;

        void Awake()
        {
            for (int i = 0; i < balls.Length; i++)
            {
                int c = i;
                balls[i].onClick.AddListener(() => Choose(c));
            }
            dim.onClick.AddListener(() => Choose(-1));
        }

        public void Show(Action<int> onChoose)
        {
            callback = onChoose;
            closing = false;
            gameObject.SetActive(true);
            group.alpha = 0f;
            group.blocksRaycasts = true;
            Tw.Alpha(group, 1f, 0.18f);
            panel.localScale = Vector3.one * 0.7f;
            Tw.Scale(panel, 1f, 0.35f, Ease.OutBack);
            for (int i = 0; i < balls.Length; i++)
            {
                balls[i].transform.localScale = Vector3.zero;
                Tw.Scale(balls[i].transform, 1f, 0.35f, Ease.OutBackStrong, 0.06f + i * 0.05f);
            }
        }

        public void Choose(int color)
        {
            if (!IsOpen) return;
            var cb = callback;
            callback = null;
            closing = true;
            group.blocksRaycasts = false;
            if (color >= 0) Tw.Punch(balls[color].transform, 0.2f, 0.25f);
            Tw.Alpha(group, 0f, 0.18f, Ease.OutQuad, color >= 0 ? 0.12f : 0f).OnComplete(() => gameObject.SetActive(false));
            cb?.Invoke(color);
        }
    }
}
