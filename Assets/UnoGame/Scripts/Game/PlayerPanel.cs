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
    /// <summary>Avatar, name, card count, turn ring/timer and emote bubble for one player.</summary>
    public sealed class PlayerPanel : MonoBehaviour
    {
        public bool isMe;
        public Image avatar;
        public Image ring;
        public RectTransform nameRow;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI countText;
        public RectTransform timerRoot;
        public Image timerFill;
        public RectTransform turnPill;
        public RectTransform bubble;
        public Image bubbleEmote;

        bool turn;
        int count = -1;
        float timerWidth;
        Coroutine bubbleRoutine;

        public RectTransform Anchor => avatar.rectTransform;

        public void Setup(PlayerInfo p)
        {
            avatar.sprite = ArtLibrary.I.Avatar(p.avatar);
            nameText.text = p.name;
            timerWidth = ((RectTransform)timerFill.transform.parent).rect.width;
            SetTurn(false, true);
            bubble.gameObject.SetActive(false);
        }

        public void SetTurn(bool on, bool instant = false)
        {
            if (turn == on && !instant) return;
            turn = on;
            if (instant)
            {
                var c = ring.color; c.a = on ? 1f : 0f; ring.color = c;
            }
            else
            {
                Tw.Alpha(ring, on ? 1f : 0f, 0.25f);
                if (on) Tw.Punch(avatar.transform, 0.12f, 0.4f);
            }
            timerRoot.gameObject.SetActive(on);
            if (turnPill != null)
            {
                turnPill.gameObject.SetActive(on);
                nameRow.gameObject.SetActive(!on);
                if (on && !instant) { turnPill.localScale = Vector3.one * 0.6f; Tw.Scale(turnPill, 1f, 0.35f, Ease.OutBack); }
            }
        }

        /// <summary>k = fraction of the turn left (1 .. 0).</summary>
        public void SetTimer(float k)
        {
            k = Mathf.Clamp01(k);
            if (timerWidth <= 0f) timerWidth = ((RectTransform)timerFill.transform.parent).rect.width;
            var rt = timerFill.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Max(12f, timerWidth * k), rt.sizeDelta.y);
            timerFill.color = k > 0.33f ? Palette.Gold : Color.Lerp(Palette.Red, Palette.Gold, k / 0.33f);
        }

        public void SetCount(int n, bool animate = true)
        {
            if (countText == null || n == count) return;
            count = n;
            countText.text = n.ToString();
            if (animate) Tw.Punch(countText.transform, 0.3f, 0.3f);
        }

        public void SetName(PlayerInfo p)
        {
            nameText.text = p.name;
            avatar.sprite = ArtLibrary.I.Avatar(p.avatar);
        }

        public void ShowEmote(int e)
        {
            if (bubbleRoutine != null) StopCoroutine(bubbleRoutine);
            bubbleRoutine = StartCoroutine(BubbleRoutine(e));
        }

        IEnumerator BubbleRoutine(int e)
        {
            bubbleEmote.sprite = ArtLibrary.I.emotes[Mathf.Clamp(e, 0, ArtLibrary.I.emotes.Length - 1)];
            bubble.gameObject.SetActive(true);
            bubble.localScale = Vector3.zero;
            yield return Tw.Scale(bubble, 1f, 0.35f, Ease.OutBackStrong).Wait();
            Tw.Punch(bubbleEmote.transform, 0.18f, 0.5f);
            yield return Tw.Wait(2.0f);
            yield return Tw.Scale(bubble, 0f, 0.2f, Ease.InBack).Wait();
            bubble.gameObject.SetActive(false);
            bubbleRoutine = null;
        }
    }
}
