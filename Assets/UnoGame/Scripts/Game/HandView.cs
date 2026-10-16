using System;
using System.Collections.Generic;
using UnoGame.UI;
using UnityEngine;

namespace UnoGame.Game
{
    /// <summary>
    /// A fanned row of cards. The local hand is sorted by colour/value and shown face up; the 1v1 opponent's
    /// hand is drawn upside down at the top of the screen. At bigger tables each opponent has a small upright
    /// fan (fanUp) peeking over their avatar.
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        public CardView prefab;
        public bool opponent;
        public bool fanUp;
        public float cardScale = 1f;
        public float maxWidth = 1000f;
        public float spacingMax = 140f;
        public float liftHeight = 26f;
        public float curve = 16f;

        public readonly List<CardView> Cards = new List<CardView>();
        public event Action<CardView> CardClicked;

        RectTransform Rt => (RectTransform)transform;

        public CardView Spawn(int id, bool faceUp)
        {
            var cv = Instantiate(prefab, transform);
            cv.name = "Card";
            cv.Set(id, faceUp);
            cv.Rect.localScale = Vector3.one * Scale(Cards.Count + 1);
            cv.SetInteractive(!opponent);
            if (!opponent) cv.Clicked += c => CardClicked?.Invoke(c);
            return cv;
        }

        /// <summary>Adds a card that already exists elsewhere (keeps its world position for the flight).</summary>
        public void Insert(CardView cv)
        {
            int idx = IndexFor(cv);
            Cards.Insert(idx, cv);
            cv.transform.SetParent(transform, true);
            cv.SetInteractive(!opponent);
        }

        int IndexFor(CardView cv)
        {
            if (opponent) return Cards.Count;
            int idx = 0;
            while (idx < Cards.Count && Cards[idx].SortKey <= cv.SortKey) idx++;
            return idx;
        }

        public void Remove(CardView cv) => Cards.Remove(cv);

        /// <summary>Re-sort after the faces changed (UNO Flip turned the cards over).</summary>
        public void Resort()
        {
            if (opponent) return;
            Cards.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));
        }

        public CardView Find(int id)
        {
            foreach (var c in Cards) if (c.Id == id) return c;
            return null;
        }

        /// <summary>Opponent plays: take a card from near the middle of their fan.</summary>
        public CardView TakeAny()
        {
            if (Cards.Count == 0) return null;
            var cv = Cards[Cards.Count / 2];
            Cards.Remove(cv);
            return cv;
        }

        public void Clear()
        {
            foreach (var c in Cards) if (c != null) Destroy(c.gameObject);
            Cards.Clear();
        }

        float Scale(int n) => cardScale * (opponent ? 1f : n > 18 ? 0.86f : n > 13 ? 0.93f : 1f);

        public void Slot(int i, int n, out Vector2 pos, out float rot, out float scale)
        {
            scale = Scale(n);
            float w = CardView.Width * scale;
            float avail = Mathf.Min(maxWidth, Mathf.Max(300f, Rt.rect.width - 30f));
            float spacing = n > 1 ? Mathf.Min(spacingMax * scale, (avail - w) / (n - 1)) : 0f;
            float mid = (n - 1) / 2f;
            float off = i - mid;
            float step = n > 1 ? Mathf.Min(3.2f, 26f / n) : 0f;
            float drop = n > 1 ? curve * (off * off) / Mathf.Max(1f, mid * mid) : 0f;
            if (fanUp)
            {
                // a small hand held up behind the avatar, spread like a real fan
                float fan = n > 1 ? Mathf.Min(12f, 48f / n) : 0f;
                pos = new Vector2(off * spacing, -drop);
                rot = -off * fan;
            }
            else if (opponent)
            {
                pos = new Vector2(-off * spacing, drop);
                rot = 180f + off * step;
            }
            else
            {
                pos = new Vector2(off * spacing, -drop);
                rot = -off * step;
            }
        }

        public void Layout(float duration = 0.28f)
        {
            int n = Cards.Count;
            for (int i = 0; i < n; i++)
            {
                var cv = Cards[i];
                Slot(i, n, out var pos, out var rot, out var scale);
                if (cv.Lifted) pos.y += liftHeight;
                cv.transform.SetSiblingIndex(i);
                if (duration <= 0f)
                {
                    Tw.Kill(cv.Rect);
                    cv.Rect.anchoredPosition = pos;
                    cv.Rect.localEulerAngles = new Vector3(0, 0, rot);
                    cv.Rect.localScale = Vector3.one * scale;
                }
                else
                {
                    Tw.Pos(cv.Rect, pos, duration, Ease.OutCubic);
                    Tw.Rot(cv.Rect, rot, duration, Ease.OutCubic);
                    Tw.Scale(cv.Rect, scale, duration, Ease.OutCubic);
                }
            }
        }
    }
}
