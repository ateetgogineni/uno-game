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
    /// <summary>Big centred title ("YOU GO FIRST!", "YOU WON!").</summary>
    public sealed class Banner : MonoBehaviour
    {
        public CanvasGroup group;
        public TextMeshProUGUI text;
        public Image rays;

        public IEnumerator Show(string message, Color color, float hold, bool withRays)
        {
            gameObject.SetActive(true);
            text.text = message;
            text.color = color;
            rays.gameObject.SetActive(withRays);
            group.alpha = 0f;
            text.transform.localScale = Vector3.one * 2.2f;
            Tw.Alpha(group, 1f, 0.15f);
            yield return Tw.Scale(text.transform, 1f, 0.45f, Ease.OutBackStrong).Wait();
            yield return Tw.Wait(hold);
            yield return Tw.Alpha(group, 0f, 0.3f).Wait();
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (rays.gameObject.activeSelf) rays.transform.Rotate(0, 0, -25f * Time.unscaledDeltaTime);
        }
    }
}
