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
    /// <summary>Short message pill.</summary>
    public sealed class Toast : MonoBehaviour
    {
        public CanvasGroup group;
        public TextMeshProUGUI text;
        public RectTransform pill;
        Coroutine routine;

        public void Show(string message, float seconds = 2.2f)
        {
            if (routine != null) StopCoroutine(routine);
            gameObject.SetActive(true);
            routine = StartCoroutine(Run(message, seconds));
        }

        IEnumerator Run(string message, float seconds)
        {
            text.text = message;
            pill.sizeDelta = new Vector2(Mathf.Clamp(text.preferredWidth + 90f, 300f, 1000f), pill.sizeDelta.y);
            group.alpha = 0f;
            pill.localScale = Vector3.one * 0.85f;
            Tw.Alpha(group, 1f, 0.2f);
            Tw.Scale(pill, 1f, 0.3f, Ease.OutBack);
            yield return Tw.Wait(seconds);
            yield return Tw.Alpha(group, 0f, 0.25f).Wait();
            gameObject.SetActive(false);
            routine = null;
        }
    }
}
