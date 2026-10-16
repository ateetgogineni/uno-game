using System;
using System.Collections.Generic;
using TMPro;
using UnoGame.Core;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Game
{
    /// <summary>
    /// "Who?" dialog for the cards that hit a player of your choice at a table of 3+: Swap / Trade Hands,
    /// the No Mercy 7 and Targeted Draw. Built at runtime (the table size is only known once matched).
    /// </summary>
    public sealed class TargetPicker : MonoBehaviour
    {
        CanvasGroup group;
        RectTransform panel;
        TextMeshProUGUI title;
        RectTransform grid;
        Action<int> callback;
        bool closing;
        bool landscape;
        readonly List<(Button button, int seat, int count)> options = new List<(Button, int, int)>();

        const float CellW = 200f, CellH = 250f;

        public bool IsOpen => gameObject.activeSelf && !closing;

        /// <summary>The option with the fewest cards (what the autopilot picks).</summary>
        public int Best
        {
            get
            {
                int best = -1, bestCount = int.MaxValue;
                foreach (var o in options) if (o.count < bestCount) { best = o.seat; bestCount = o.count; }
                return best;
            }
        }

        public static TargetPicker Create(RectTransform overlays, bool landscape)
        {
            var root = UIKit.Node("TargetPicker", overlays).Fill();
            var tp = root.gameObject.AddComponent<TargetPicker>();
            tp.landscape = landscape;
            tp.group = UIKit.Group(root);
            var dim = UIKit.Dim(root, 0.6f).gameObject.AddComponent<Button>();
            dim.transition = Selectable.Transition.None;
            dim.onClick.AddListener(() => tp.Choose(-1));
            tp.panel = UIKit.Img(root, "Panel", UIKit.S("panel"), new Color(1, 1, 1, 0.97f), true).rectTransform;
            tp.panel.At(new Vector2(0.5f, 0.5f), landscape ? Vector2.zero : new Vector2(0, 170), new Vector2(760, 700));
            tp.title = UIKit.Txt(tp.panel, "Title", "Swap hands with...", 46, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            tp.title.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(700, 70));
            tp.title.enableAutoSizing = true;
            tp.title.fontSizeMin = 30;
            tp.title.fontSizeMax = 46;
            tp.grid = UIKit.Node("Grid", tp.panel).At(new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(700, 500));
            // the dialog sits under the confirm dialog but over everything else
            root.SetSiblingIndex(Mathf.Max(0, overlays.childCount - 2));
            root.gameObject.SetActive(false);
            return tp;
        }

        public void Show(string heading, List<(int seat, PlayerInfo info, int count)> list, Action<int> onPick)
        {
            callback = onPick;
            closing = false;
            title.text = heading;
            foreach (var o in options) Destroy(o.button.gameObject);
            options.Clear();

            int n = list.Count;
            int cols = landscape ? Mathf.Min(n, 5) : Mathf.Min(n, 3);
            int rows = Mathf.CeilToInt(n / (float)cols);
            float w = cols * CellW + 80f, h = rows * CellH + 170f;
            panel.sizeDelta = new Vector2(Mathf.Max(560f, w), h);
            grid.sizeDelta = new Vector2(cols * CellW, rows * CellH);
            grid.anchoredPosition = new Vector2(0, -50);
            for (int i = 0; i < n; i++)
            {
                int r = i / cols, c = i % cols;
                int inRow = Mathf.Min(cols, n - r * cols);
                float x = (c - (inRow - 1) / 2f) * CellW;
                float y = ((rows - 1) / 2f - r) * CellH;
                var e = list[i];
                options.Add((Option(e.seat, e.info, e.count, new Vector2(x, y), i), e.seat, e.count));
            }

            gameObject.SetActive(true);
            group.alpha = 0f;
            group.blocksRaycasts = true;
            Tw.Alpha(group, 1f, 0.18f);
            panel.localScale = Vector3.one * 0.7f;
            Tw.Scale(panel, landscape ? 0.92f : 1f, 0.35f, Ease.OutBack);
        }

        Button Option(int seat, PlayerInfo info, int count, Vector2 pos, int index)
        {
            var b = UIKit.Btn(grid, "Seat" + seat, UIKit.S("rounded"), new Vector2(CellW - 20, CellH - 20));
            var bg = b.GetComponent<Image>();
            bg.color = new Color(1, 1, 1, 0.08f);
            bg.pixelsPerUnitMultiplier = 2.2f;
            var rt = (RectTransform)b.transform;
            rt.At(new Vector2(0.5f, 0.5f), pos, new Vector2(CellW - 20, CellH - 20));
            var ring = UIKit.Img(rt, "Ring", UIKit.S("circle"), new Color(0.07f, 0.09f, 0.11f, 0.9f));
            ring.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(136, 136));
            var av = UIKit.Img(rt, "Avatar", ArtLibrary.I.Avatar(info.avatar), Color.white);
            av.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(124, 124));
            var name = UIKit.Txt(rt, "Name", info.name, 28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            name.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, -62), new Vector2(CellW - 30, 40));
            name.enableAutoSizing = true;
            name.fontSizeMin = 18;
            name.fontSizeMax = 28;
            var cards = UIKit.Txt(rt, "Count", count == 1 ? "1 card" : count + " cards", 24,
                count <= 2 ? new Color(1f, 0.5f, 0.45f) : new Color(1, 1, 1, 0.65f), TextAlignmentOptions.Center, FontStyles.Bold);
            cards.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, -96), new Vector2(CellW - 30, 34));
            b.onClick.AddListener(() => Choose(seat));
            rt.localScale = Vector3.zero;
            Tw.Scale(rt, 1f, 0.35f, Ease.OutBackStrong, 0.05f + index * 0.04f);
            return b;
        }

        public void Choose(int seat)
        {
            if (!IsOpen) return;
            var cb = callback;
            callback = null;
            closing = true;
            group.blocksRaycasts = false;
            foreach (var o in options) if (o.seat == seat) Tw.Punch(o.button.transform, 0.15f, 0.25f);
            Tw.Alpha(group, 0f, 0.18f, Ease.OutQuad, seat >= 0 ? 0.12f : 0f).OnComplete(() => gameObject.SetActive(false));
            App.Play(seat >= 0 ? Sound.Click : Sound.Pop, 0.8f);
            cb?.Invoke(seat);
        }
    }
}
