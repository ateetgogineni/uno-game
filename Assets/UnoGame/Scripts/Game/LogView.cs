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
    /// <summary>Three-line game log: newest at the bottom in full white, older lines fade out.</summary>
    public sealed class LogView : MonoBehaviour
    {
        public TextMeshProUGUI[] lines = new TextMeshProUGUI[3];
        readonly string[] texts = { "", "", "" };
        static readonly float[] Alphas = { 0.32f, 0.62f, 1f };
        static readonly float[] Sizes = { 34f, 37f, 42f };

        public void Clear()
        {
            for (int i = 0; i < 3; i++) { texts[i] = ""; lines[i].text = ""; }
        }

        public void Push(string text)
        {
            texts[0] = texts[1];
            texts[1] = texts[2];
            texts[2] = text;
            for (int i = 0; i < 3; i++)
            {
                lines[i].text = texts[i];
                lines[i].fontSizeMax = Sizes[i];
                lines[i].fontSize = Sizes[i];
                var c = lines[i].color; c.a = Alphas[i]; lines[i].color = c;
            }
            var newest = lines[2];
            var c2 = newest.color; c2.a = 0f; newest.color = c2;
            Tw.Alpha(newest, 1f, 0.25f);
            var rt = newest.rectTransform;
            Vector2 home = rt.anchoredPosition;
            rt.anchoredPosition = home + new Vector2(0, -18f);
            Tw.Pos(rt, home, 0.25f, Ease.OutCubic);
        }
    }
}
