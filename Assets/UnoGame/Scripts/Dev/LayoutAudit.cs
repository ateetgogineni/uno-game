using System.Collections.Generic;
using System.Text;
using UnoGame.Game;
using UnoGame.Home;
using UnityEngine;

namespace UnoGame.Dev
{
    /// <summary>
    /// Measures key UI elements in screen pixels and reports anything off-screen or overlapping that
    /// should not. Used by the autopilot on every test resolution.
    /// </summary>
    public static class LayoutAudit
    {
        struct Item { public string name; public Rect rect; public bool mayLeaveScreen; }

        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = Mathf.Min(c[0].x, c[1].x, c[2].x, c[3].x), xMax = Mathf.Max(c[0].x, c[1].x, c[2].x, c[3].x);
            float yMin = Mathf.Min(c[0].y, c[1].y, c[2].y, c[3].y), yMax = Mathf.Max(c[0].y, c[1].y, c[2].y, c[3].y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static void Add(List<Item> list, string name, Transform t, bool mayLeave = false)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return;
            list.Add(new Item { name = name, rect = ScreenRect((RectTransform)t), mayLeaveScreen = mayLeave });
        }

        public static void Game(GameController gc)
        {
            var items = new List<Item>();
            Add(items, "BackButton", gc.backButton.transform);
            Add(items, "MenuButton", gc.menuButton.transform);
            Add(items, "OppAvatar", gc.oppPanel.avatar.transform);
            Add(items, "OppName", gc.oppPanel.nameRow);
            Add(items, "OppCount", gc.oppPanel.countText.transform);
            Add(items, "DrawPile", gc.drawPile);
            Add(items, "Discard", gc.discardPile);
            Add(items, "ArrowA", gc.arrowA);
            Add(items, "ArrowB", gc.arrowB);
            for (int i = 0; i < 3; i++)
            {
                var line = gc.log.lines[i];
                if (string.IsNullOrEmpty(line.text)) continue;
                var r = ScreenRect(line.rectTransform);
                float w = line.textBounds.size.x * line.rectTransform.lossyScale.x;
                var centre = r.center;
                items.Add(new Item { name = "LogLine" + i, rect = new Rect(centre.x - w / 2, r.y, w, r.height) });
            }
            Add(items, "MeAvatar", gc.mePanel.avatar.transform);
            Add(items, "TurnPill", gc.mePanel.turnPill);
            Add(items, "MeName", gc.mePanel.nameRow);
            Add(items, "EmoteButton", gc.emoteButton.transform);
            Add(items, "UnoButton", gc.unoButton.transform);
            Add(items, "PassButton", gc.passButton.transform);
            for (int i = 0; i < gc.myHand.Cards.Count; i++) Add(items, "HandCard" + i, gc.myHand.Cards[i].face.transform);
            for (int i = 0; i < gc.oppHand.Cards.Count; i++) Add(items, "OppCard" + i, gc.oppHand.Cards[i].face.transform, true);
            Report("game", items, (a, b) =>
            {
                bool aHand = a.StartsWith("HandCard"), bHand = b.StartsWith("HandCard");
                bool aOpp = a.StartsWith("OppCard"), bOpp = b.StartsWith("OppCard");
                if ((aHand && bHand) || (aOpp && bOpp)) return false;      // cards in a fan overlap by design
                if ((a == "DrawPile" && b == "Discard") || (a == "Discard" && b == "DrawPile")) return false;
                if ((aOpp && (b == "BackButton" || b == "MenuButton")) || (bOpp && (a == "BackButton" || a == "MenuButton"))) return false;
                return true;
            });
        }

        public static void Home(HomeScreen home)
        {
            var items = new List<Item>();
            Add(items, "Profile", home.profileButton.transform);
            Add(items, "Wins", home.winsText.transform.parent);
            Add(items, "Sound", home.soundButton.transform);
            Add(items, "Logo", home.logo);
            Add(items, "Play", home.playButton.transform);
            Add(items, "Online", home.onlineText.transform);
            Add(items, "Bottom", home.bottomInfo);
            Report("home", items, (a, b) => true);
        }

        static void Report(string screen, List<Item> items, System.Func<string, string, bool> mustNotOverlap)
        {
            var sb = new StringBuilder();
            int problems = 0;
            var screenRect = new Rect(0, 0, Screen.width, Screen.height);
            foreach (var it in items)
            {
                if (it.mayLeaveScreen) continue;
                if (it.rect.xMin < -1 || it.rect.yMin < -1 || it.rect.xMax > screenRect.width + 1 || it.rect.yMax > screenRect.height + 1)
                {
                    problems++;
                    sb.AppendLine($"  OFFSCREEN {it.name} {Fmt(it.rect)}");
                }
            }
            for (int i = 0; i < items.Count; i++)
                for (int j = i + 1; j < items.Count; j++)
                {
                    var a = items[i]; var b = items[j];
                    if (!mustNotOverlap(a.name, b.name)) continue;
                    if (!a.rect.Overlaps(b.rect)) continue;
                    float ow = Mathf.Min(a.rect.xMax, b.rect.xMax) - Mathf.Max(a.rect.xMin, b.rect.xMin);
                    float oh = Mathf.Min(a.rect.yMax, b.rect.yMax) - Mathf.Max(a.rect.yMin, b.rect.yMin);
                    if (ow < 2 || oh < 2) continue;
                    problems++;
                    sb.AppendLine($"  OVERLAP {a.name} x {b.name} ({ow:0}x{oh:0}px)");
                }
            Debug.Log($"[LAYOUT] {screen} {Screen.width}x{Screen.height}: {(problems == 0 ? "OK" : problems + " problem(s)")}, {items.Count} elements checked\n{sb}");
        }

        static string Fmt(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";
    }
}
