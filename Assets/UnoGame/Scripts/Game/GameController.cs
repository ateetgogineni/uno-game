using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnoGame.Core;
using UnoGame.Net;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Game
{
    /// <summary>
    /// Drives the table for every game mode. Messages from the session (online server or local bot game)
    /// are queued and played back one event at a time as animations; after each batch the authoritative
    /// state is applied (hand, counts, top card, turn, timer, Flip side). Taps on cards are animated straight
    /// away (optimistic) and then confirmed by the server's event.
    /// </summary>
    public sealed partial class GameController : MonoBehaviour
    {
        [Header("Table")]
        public CardView cardPrefab;
        public HandView myHand;
        public HandView oppHand;
        public RectTransform drawPile;
        public Button drawButton;
        public Image deckTop;
        public Image[] deckLayers;
        public Image launcherImg;
        public RectTransform discardPile;
        public Image colorGlow;
        public Image deckGlow;
        public RectTransform arrowA, arrowB;
        public RectTransform stackBadge;
        public TextMeshProUGUI stackText;
        public RectTransform fxLayer;
        public RectTransform tableCenter;
        public Image tableBg;
        public RectTransform modeChip;
        public TextMeshProUGUI modeText;
        public Image modeIcon;

        [Header("Players")]
        public PlayerPanel mePanel;
        public PlayerPanel oppPanel;

        [Header("HUD")]
        public LogView log;
        public Button unoButton;
        public Image unoGlow;
        public Button passButton;
        public Button emoteButton;
        public Button backButton;
        public Button menuButton;
        public RectTransform reconnectBanner;

        [Header("Overlays")]
        public ColorPicker picker;
        public EmotePanel emotePanel;
        public ResultPanel result;
        public ConfirmDialog dialog;
        public Toast toast;
        public Banner banner;
        public MenuPanel menu;
        public DarePanel dare;

        public const float DiscardScale = 1.16f;
        public const float DeckScale = 0.9f;
        static readonly Color DarkTable = new Color(0.62f, 0.52f, 0.86f);

        GameSession session;
        St state;
        int seat;
        bool busy;
        bool awaiting;
        int pendingPlay = -1;
        bool unoArmed;
        bool unoHintShown;
        bool mercyWarned;
        bool rouletteAsked;
        bool dareAsked;
        bool over;
        bool leaving;
        float deadline;
        float turnLen = 15f;
        int lastTurn = -1;
        int lastTick = -1;
        CardView discardTop;
        readonly List<CardView> discardViews = new List<CardView>();

        public St State => state;
        public GameSession Session => session;
        public bool IsOver => over;
        public IReadOnlyList<CardView> MyCards => myHand.Cards;
        public bool PickerOpen => picker.IsOpen;
        public bool ResultOpen => result.IsOpen;
        public bool DareOpen => dare.IsOpen;

        public bool MyTurn => state != null && !over && state.turn == seat;

        /// <summary>True when the local player may act right now.</summary>
        public bool CanAct => MyTurn && !busy && !awaiting && pendingPlay < 0 && session != null && session.Inbox.Count == 0 &&
                              !picker.IsOpen && !dialog.IsOpen && !dare.IsOpen && !leaving;

        void Start()
        {
            session = App.I.Match.Session;
            if (session == null)
            {
                // Scene opened on its own (editor): start a quick offline match.
                App.I.Config.forceOffline = true;
                App.I.Match.StartSearch(App.I.SelectedMode);
                StartCoroutine(WaitForSession());
                return;
            }
            Begin();
        }

        IEnumerator WaitForSession()
        {
            while (App.I.Match.Session == null) yield return null;
            session = App.I.Match.Session;
            Begin();
        }

        void Begin()
        {
            seat = session.Seat;
            Table.Mode = session.Mode;
            Table.Side = 0;
            mePanel.Setup(session.Me);
            oppPanel.Setup(session.Opponent);
            oppPanel.SetCount(0, false);
            log.Clear();
            stackBadge.gameObject.SetActive(false);
            passButton.gameObject.SetActive(false);
            unoButton.gameObject.SetActive(false);
            reconnectBanner.gameObject.SetActive(false);
            SetGlowAlpha(deckGlow, 0f);
            SetGlowAlpha(colorGlow, 0f);
            SetupModeVisuals();

            myHand.CardClicked += OnCardClicked;
            drawButton.onClick.AddListener(OnDeckClicked);
            passButton.onClick.AddListener(OnPassClicked);
            unoButton.onClick.AddListener(OnUnoClicked);
            emoteButton.onClick.AddListener(() => emotePanel.Toggle());
            emotePanel.Picked += OnEmotePicked;
            backButton.onClick.AddListener(AskLeave);
            menuButton.onClick.AddListener(() => menu.Show());
            menu.leaveButton.onClick.AddListener(() => { menu.Hide(); AskLeave(); });
            result.playAgain.onClick.AddListener(PlayAgain);
            result.home.onClick.AddListener(GoHome);

            session.EmoteReceived += OnEmote;
            session.ConnectionChanged += OnConnectionChanged;
            session.Lost += OnLost;
            session.PlayersChanged += () => oppPanel.SetName(session.Opponent);

            session.Ready();
            StartCoroutine(Loop());
        }

        void SetupModeVisuals()
        {
            bool attack = Table.Mode == "attack";
            launcherImg.gameObject.SetActive(attack);
            foreach (var img in deckLayers) img.enabled = !attack;
            var c = deckTop.color;
            c.a = attack ? 0f : 1f;
            deckTop.color = c;
            if (tableBg != null) tableBg.color = Color.white;
            UpdateModeChip(false);
        }

        void UpdateModeChip(bool animate)
        {
            string text;
            switch (Table.Mode)
            {
                case "flip": text = Table.Side == 1 ? "DARK SIDE" : "LIGHT SIDE"; break;
                case "nomercy": text = "NO MERCY  •  25 = OUT"; break;
                case "allwild": text = "ALL WILD"; break;
                case "attack": text = "ATTACK"; break;
                case "dare": text = "DARE"; break;
                default: text = "CLASSIC"; break;
            }
            modeText.text = text;
            var info = ModeInfo.Get(Table.Mode);
            modeIcon.color = Table.Mode == "flip" && Table.Side == 1 ? new Color(0.72f, 0.54f, 1f) : info.Accent;
            if (animate) Tw.Punch(modeChip, 0.2f, 0.4f);
        }

        void OnDestroy()
        {
            if (session == null) return;
            session.EmoteReceived -= OnEmote;
            session.ConnectionChanged -= OnConnectionChanged;
            session.Lost -= OnLost;
        }

        // ---------------------------------------------------------------- message loop

        IEnumerator Loop()
        {
            while (true)
            {
                if (session.Inbox.Count == 0) { yield return null; continue; }
                var m = session.Inbox.Dequeue();
                switch (m.t)
                {
                    case "up":
                        busy = true;
                        if (m.ev != null) yield return Guard(PlayEvents(m.ev));
                        if (m.st != null) ApplyState(m.st, m.receivedAt);
                        busy = false;
                        break;
                    case "sync":
                        if (m.players != null && m.players.Length == 2) session.Players = m.players;
                        Rebuild(m.st);
                        ApplyState(m.st, m.receivedAt);
                        break;
                    case "err":
                        Debug.Log("[Game] server rejected move: " + m.msg);
                        pendingPlay = -1;
                        awaiting = false;
                        break;
                }
            }
        }

        /// <summary>
        /// Runs an animation coroutine (flattening nested IEnumerators) and stops it cleanly if it throws,
        /// so one bad animation can never freeze the table; the state applied afterwards fixes the view.
        /// </summary>
        static IEnumerator Guard(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                bool moved;
                object current = null;
                try
                {
                    moved = top.MoveNext();
                    if (moved) current = top.Current;
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                    yield break;
                }
                if (!moved) { stack.Pop(); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
        }

        IEnumerator PlayEvents(Ev[] events)
        {
            int[] myDeal = null;
            int oppDeal = 0;
            for (int i = 0; i < events.Length; i++)
            {
                var ev = events[i];
                switch (ev.k)
                {
                    case "deal":
                        if (ev.p == seat) myDeal = ev.cards;
                        else oppDeal = ev.n;
                        bool lastDeal = i + 1 >= events.Length || events[i + 1].k != "deal";
                        if (lastDeal) yield return DealRoutine(myDeal ?? new int[0], oppDeal);
                        break;
                    case "start": yield return StartCardRoutine(ev.card, ev.color); break;
                    case "turn": ShowTurn(ev.p); break;
                    case "play": yield return PlayRoutine(ev); break;
                    case "uno": yield return UnoRoutine(ev.p); break;
                    case "unoPen": yield return PenaltyRoutine(ev); break;
                    case "reverse": yield return ReverseRoutine(ev.dir); break;
                    case "skip": yield return SkipRoutine(ev.p, Mathf.Max(1, ev.n)); break;
                    case "stack": ShowStack(ev.n, true); break;
                    case "draw": yield return DrawRoutine(ev); break;
                    case "reshuffle": yield return ReshuffleRoutine(); break;
                    case "flip": yield return FlipRoutine(ev.n, ev.card, ev.color); break;
                    case "swap": yield return SwapRoutine(ev.p); break;
                    case "discardAll": yield return DiscardAllRoutine(ev); break;
                    case "launch": yield return LaunchRoutine(ev); break;
                    case "roulettePending":
                        App.Play(Sound.Whoosh, 0.7f);
                        log.Push("<color=#FFD447>Color Roulette!</color> " + NameOf(ev.p) + (ev.p == seat ? " name" : " names") + " a colour");
                        break;
                    case "roulette": yield return RouletteRoutine(ev); break;
                    case "darePending":
                        App.Play(Sound.Uno, 0.6f, 0.8f);
                        log.Push("<color=#3FE0DD>Dare!</color> " + NameOf(ev.p) + ": do the dare or draw " + ev.n);
                        break;
                    case "dare": yield return DareResultRoutine(ev); break;
                    case "mercy":
                        App.Play(Sound.Error);
                        toast.Show((ev.p == seat ? "You hit " : NameOf(ev.p) + " hit ") + ev.n + " cards: Mercy rule!", 2.6f);
                        log.Push("<color=#FF5C5C>Mercy rule:</color> " + NameOf(ev.p) + (ev.p == seat ? " are" : " is") + " out");
                        yield return Tw.Wait(0.8f);
                        break;
                    case "win": yield return WinRoutine(ev); break;
                }
            }
        }

        // ---------------------------------------------------------------- helpers

        string NameOf(int p) => p == seat ? "You" : session.Players[p].name;
        PlayerPanel PanelOf(int p) => p == seat ? mePanel : oppPanel;
        Vector3 DeckWorld => drawPile.position;
        HandView HandOf(int p) => p == seat ? myHand : oppHand;

        CardView SpawnAtDeck(HandView hand, int id)
        {
            var cv = hand.Spawn(id, false);
            cv.Rect.position = DeckWorld;
            cv.Rect.localEulerAngles = Vector3.zero;
            cv.Rect.localScale = Vector3.one * DeckScale;
            return cv;
        }

        static void SetGlowAlpha(Image img, float a)
        {
            var c = img.color; c.a = a; img.color = c;
        }

        void SetColor(int color, bool animate)
        {
            var c = color >= 0 && color < 4 ? Palette.ForCard(color, Table.Side) : new Color(1f, 1f, 1f);
            float a = color >= 0 && color < 4 ? 0.55f : 0.25f;
            if (animate)
            {
                Tw.Color(colorGlow, new Color(c.r, c.g, c.b, a), 0.35f);
                colorGlow.transform.localScale = Vector3.one * 0.8f;
                Tw.Scale(colorGlow.transform, 1f, 0.45f, Ease.OutBack);
            }
            else colorGlow.color = new Color(c.r, c.g, c.b, a);
        }

        void SetArrows(int dir, bool animate)
        {
            float sx = dir >= 0 ? 1f : -1f;
            foreach (var a in new[] { arrowA, arrowB })
            {
                var target = new Vector3(sx * Mathf.Abs(a.localScale.x), a.localScale.y, 1f);
                if (animate) Tw.Scale(a, target, 0.45f, Ease.OutBack);
                else a.localScale = target;
            }
        }

        void ShowStack(int n, bool animate)
        {
            bool on = n > 0;
            if (!on) { stackBadge.gameObject.SetActive(false); return; }
            stackText.text = "+" + n;
            if (!stackBadge.gameObject.activeSelf || animate)
            {
                stackBadge.gameObject.SetActive(true);
                stackBadge.localScale = Vector3.zero;
                Tw.Scale(stackBadge, 1f, 0.4f, Ease.OutBackStrong);
                if (animate) App.Play(Sound.Plus);
            }
        }

        void ShowTurn(int p)
        {
            mePanel.SetTurn(!over && p == seat);
            oppPanel.SetTurn(!over && p != seat);
        }

        // ---------------------------------------------------------------- event animations

        IEnumerator DealRoutine(int[] mine, int oppCount)
        {
            App.Play(Sound.Shuffle);
            log.Push("<color=#FFD447>" + ModeInfo.Get(Table.Mode).Title + "</color>");
            yield return Tw.Wait(0.25f);
            int rounds = Mathf.Max(mine.Length, oppCount);
            for (int r = 0; r < rounds; r++)
            {
                if (r < mine.Length)
                {
                    var cv = SpawnAtDeck(myHand, mine[r]);
                    myHand.Insert(cv);
                    myHand.Layout(0.32f);
                    cv.Flip(true, mine[r], 0.24f, 0.1f);
                    App.Play(Sound.Deal, 0.8f, 1f + r * 0.02f);
                }
                if (r < oppCount)
                {
                    var cv = SpawnAtDeck(oppHand, -1);
                    oppHand.Insert(cv);
                    oppHand.Layout(0.32f);
                    oppPanel.SetCount(oppHand.Cards.Count);
                }
                yield return Tw.Wait(0.11f);
            }
            yield return Tw.Wait(0.3f);
        }

        IEnumerator StartCardRoutine(int card, int color)
        {
            var cv = Instantiate(cardPrefab, discardPile);
            cv.Set(card, false);
            cv.SetInteractive(false);
            cv.Rect.position = DeckWorld;
            cv.Rect.localScale = Vector3.one * DeckScale;
            yield return FlyToDiscard(cv, card);
            SetColor(color, true);
            if (state == null) log.Push("First card: " + Cards.RichName(Table.FaceOf(card), Table.Side));
        }

        IEnumerator FlyToDiscard(CardView cv, int id, bool underTop = false)
        {
            cv.transform.SetParent(discardPile, true);
            if (underTop && discardTop != null) cv.transform.SetSiblingIndex(discardTop.transform.GetSiblingIndex());
            else cv.transform.SetAsLastSibling();
            cv.SetDim(false, true);
            cv.SetGlow(Color.white, false);
            cv.SetInteractive(false);
            cv.Lifted = false;
            float rot = Random.Range(-16f, 16f);
            Vector2 offset = new Vector2(Random.Range(-8f, 8f), Random.Range(-6f, 6f));
            const float dur = 0.38f;
            Tw.Pos(cv.Rect, offset, dur, Ease.OutCubic);
            Tw.Rot(cv.Rect, rot, dur, Ease.OutCubic);
            Tw.Scale(cv.Rect, DiscardScale, dur, Ease.OutBack);
            if (!cv.FaceUp || cv.Id != id) cv.Flip(true, id, 0.3f);
            App.Play(Sound.Card, 0.9f);
            if (underTop && discardTop != null) discardViews.Insert(Mathf.Max(0, discardViews.Count - 1), cv);
            else
            {
                discardTop = cv;
                discardViews.Add(cv);
            }
            yield return Tw.Wait(dur);
            App.Play(Sound.Play, 0.9f);
            TrimDiscard();
        }

        void TrimDiscard()
        {
            while (discardViews.Count > 6)
            {
                var old = discardViews[0];
                discardViews.RemoveAt(0);
                if (old != null && old != discardTop) Destroy(old.gameObject);
            }
        }

        IEnumerator PlayRoutine(Ev ev)
        {
            bool mine = ev.p == seat;
            var face = Table.FaceOf(ev.card);
            if (mine && pendingPlay == ev.card)
            {
                pendingPlay = -1;
            }
            else if (mine)
            {
                var cv = myHand.Find(ev.card) ?? myHand.Spawn(ev.card, true);
                myHand.Remove(cv);
                myHand.Layout();
                yield return FlyToDiscard(cv, ev.card);
            }
            else
            {
                var cv = oppHand.TakeAny() ?? SpawnAtDeck(oppHand, -1);
                oppHand.Layout();
                oppPanel.SetCount(oppHand.Cards.Count);
                yield return FlyToDiscard(cv, ev.card);
            }
            SetColor(ev.color, true);
            string line = NameOf(ev.p) + " played " + Cards.RichName(face, Table.Side);
            if (face.IsWild)
            {
                line = NameOf(ev.p) + " played " + Cards.KindName(face).ToUpper();
                if (Table.Mode != "allwild") line += ", picked " + Cards.RichColor(ev.color, Table.Side);
                App.Play(Sound.Whoosh, 0.7f);
                var glow = ev.color >= 0 && ev.color < 4 ? Palette.ForCard(ev.color, Table.Side) : Color.white;
                Fx.PopSprite(fxLayer, UIKit.S("glow"), discardPile.position, new Vector2(520, 520), glow, 0.2f);
            }
            log.Push(line);
        }

        IEnumerator UnoRoutine(int p)
        {
            App.Play(Sound.Uno);
            var at = PanelOf(p).Anchor.position + new Vector3(0, p == seat ? 160f : -160f, 0) * CanvasScale;
            Fx.PopSprite(fxLayer, UIKit.S("uno_burst"), at, new Vector2(390, 240), Color.white, 0.8f);
            log.Push(NameOf(p) + " called <color=#FFD447>UNO!</color>");
            yield return Tw.Wait(0.5f);
        }

        IEnumerator PenaltyRoutine(Ev ev)
        {
            string who = NameOf(ev.p);
            toast.Show(who + " forgot to call UNO!  +" + ev.n);
            App.Play(Sound.Error);
            yield return DrawCardsRoutine(ev.p, ev.n, ev.cards, true);
            log.Push(who + " forgot UNO: <color=#FF5C5C>+" + ev.n + "</color>");
        }

        IEnumerator ReverseRoutine(int dir)
        {
            App.Play(Sound.Whoosh);
            Tw.Spin(arrowA, 360f, 0.6f, Ease.OutCubic);
            Tw.Spin(arrowB, 360f, 0.6f, Ease.OutCubic);
            SetArrows(dir, true);
            var tint = state != null && state.color >= 0 && state.color < 4 ? Palette.ForCard(state.color, Table.Side) : Color.white;
            Fx.PopSprite(fxLayer, UIKit.S("fx_reverse"), tableCenter.position, new Vector2(240, 240), tint, 0.35f, 180f);
            yield return Tw.Wait(0.45f);
        }

        IEnumerator SkipRoutine(int p, int times)
        {
            for (int i = 0; i < times; i++)
            {
                App.Play(Sound.Whoosh, 0.8f, 1.2f);
                Fx.PopSprite(fxLayer, UIKit.S("fx_skip"), PanelOf(p).Anchor.position + new Vector3(i * 60f, 0, 0) * CanvasScale, new Vector2(200, 200), Palette.Red, 0.5f);
                yield return Tw.Wait(0.35f);
            }
        }

        IEnumerator DrawRoutine(Ev ev)
        {
            if (ev.forced == 1) ShowStack(0, false);
            yield return DrawCardsRoutine(ev.p, ev.n, ev.cards, ev.forced == 1);
            string who = NameOf(ev.p);
            if (ev.n == 0) log.Push(who + (ev.p == seat ? " have" : " has") + " nothing to draw");
            else if (ev.n == 1) log.Push(who + " drew a card");
            else log.Push(who + " drew " + ev.n + " cards");
        }

        IEnumerator DrawCardsRoutine(int p, int n, int[] cards, bool penalty)
        {
            bool mine = p == seat;
            if (penalty && n >= 2)
            {
                App.Play(Sound.Plus);
                Fx.PopText(fxLayer, "+" + n, PanelOf(p).Anchor.position, Palette.Red, 120f);
            }
            float gap = n > 6 ? 0.07f : n > 4 ? 0.11f : 0.18f;
            for (int k = 0; k < n; k++)
            {
                if (mine)
                {
                    int id = cards != null && k < cards.Length ? cards[k] : -1;
                    var cv = SpawnAtDeck(myHand, id);
                    myHand.Insert(cv);
                    myHand.Layout(0.34f);
                    if (id >= 0) cv.Flip(true, id, 0.26f, 0.08f);
                }
                else
                {
                    var cv = SpawnAtDeck(oppHand, -1);
                    oppHand.Insert(cv);
                    oppHand.Layout(0.34f);
                    oppPanel.SetCount(oppHand.Cards.Count);
                }
                App.Play(Sound.Draw, 0.8f);
                yield return Tw.Wait(gap);
            }
            yield return Tw.Wait(0.22f);
        }

        IEnumerator ReshuffleRoutine()
        {
            App.Play(Sound.Shuffle);
            // everything under the top card goes back into the draw pile
            for (int i = discardViews.Count - 1; i >= 0; i--)
            {
                var cv = discardViews[i];
                if (cv == null || cv == discardTop) continue;
                cv.transform.SetParent(fxLayer, true);
                Tw.WorldPos(cv.transform, DeckWorld, 0.35f, Ease.InOutCubic, (discardViews.Count - 1 - i) * 0.05f);
                cv.Flip(false, -1, 0.2f);
                Destroy(cv.gameObject, 0.6f);
            }
            discardViews.Clear();
            if (discardTop != null) discardViews.Add(discardTop);
            log.Push("<color=#9FB3BA>Draw pile reshuffled</color>");
            yield return Tw.Wait(0.55f);
        }

        // UNO Flip: everything turns over.
        IEnumerator FlipRoutine(int side, int newTop, int color)
        {
            App.Play(Sound.Whoosh);
            App.Play(Sound.Shuffle, 0.7f);
            Fx.PopText(fxLayer, "FLIP!", tableCenter.position, side == 1 ? new Color(0.85f, 0.6f, 1f) : Palette.Gold, 150f, 0.5f);
            Table.Side = side;
            if (tableBg != null) Tw.Color(tableBg, side == 1 ? DarkTable : Color.white, 0.6f);
            foreach (var cv in myHand.Cards) cv.Flip(true, cv.Id, 0.45f);
            foreach (var cv in oppHand.Cards) cv.Flip(false, -1, 0.45f);
            Tw.Punch(drawPile, 0.12f, 0.5f);
            // the discard pile turns over too: its bottom card comes up on top
            foreach (var cv in discardViews) if (cv != null) Destroy(cv.gameObject);
            discardViews.Clear();
            discardTop = null;
            var top = Instantiate(cardPrefab, discardPile);
            top.SetInteractive(false);
            top.Set(newTop, false);
            top.Rect.anchoredPosition = Vector2.zero;
            top.Rect.localScale = Vector3.one * DiscardScale;
            top.Flip(true, newTop, 0.45f);
            discardTop = top;
            discardViews.Add(top);
            SetColor(color, true);
            UpdateModeChip(true);
            log.Push("<color=#FFD447>FLIP!</color> Now on the " + (side == 1 ? "<color=#B98AFF>Dark side</color>" : "<color=#FFFFFF>Light side</color>"));
            yield return Tw.Wait(0.5f);
            myHand.Resort();
            myHand.Layout(0.3f);
            yield return Tw.Wait(0.3f);
        }

        // Hands change owners (Forced Swap, Trade Hands, No Mercy 7/0).
        IEnumerator SwapRoutine(int p)
        {
            App.Play(Sound.Whoosh);
            Fx.PopText(fxLayer, "SWAP!", tableCenter.position, Palette.Gold, 140f, 0.4f);
            var mine = new List<CardView>(myHand.Cards);
            var theirs = new List<CardView>(oppHand.Cards);
            myHand.Cards.Clear();
            oppHand.Cards.Clear();
            Vector3 toOpp = oppHand.transform.position, toMe = myHand.transform.position;
            foreach (var cv in mine)
            {
                cv.transform.SetParent(fxLayer, true);
                Tw.WorldPos(cv.transform, toOpp, 0.5f, Ease.InOutCubic);
                cv.Flip(false, -1, 0.3f);
                Destroy(cv.gameObject, 0.55f);
            }
            foreach (var cv in theirs)
            {
                cv.transform.SetParent(fxLayer, true);
                Tw.WorldPos(cv.transform, toMe, 0.5f, Ease.InOutCubic);
                Destroy(cv.gameObject, 0.55f);
            }
            log.Push(NameOf(p) + " swapped hands!");
            yield return Tw.Wait(0.6f);
        }

        IEnumerator DiscardAllRoutine(Ev ev)
        {
            bool mine = ev.p == seat;
            if (ev.cards == null || ev.cards.Length == 0) yield break;
            App.Play(Sound.Whoosh, 0.8f);
            foreach (int id in ev.cards)
            {
                CardView cv;
                if (mine)
                {
                    cv = myHand.Find(id) ?? myHand.Spawn(id, true);
                    myHand.Remove(cv);
                }
                else
                {
                    cv = oppHand.TakeAny() ?? SpawnAtDeck(oppHand, -1);
                    oppPanel.SetCount(oppHand.Cards.Count);
                }
                StartCoroutine(FlyToDiscard(cv, id, true));
                yield return Tw.Wait(0.12f);
            }
            myHand.Layout();
            oppHand.Layout();
            log.Push(NameOf(ev.p) + " discarded " + ev.cards.Length + " more card" + (ev.cards.Length == 1 ? "" : "s"));
            yield return Tw.Wait(0.4f);
        }

        // UNO Attack: hitting the launcher.
        IEnumerator LaunchRoutine(Ev ev)
        {
            var rt = launcherImg.rectTransform;
            Tw.Shake(rt, 16f, 0.4f);
            App.Play(Sound.Click, 0.9f, 0.7f);
            yield return Tw.Wait(0.35f);
            if (ev.n == 0)
            {
                Fx.PopText(fxLayer, "click...", rt.position + new Vector3(0, 160f, 0) * CanvasScale, new Color(1, 1, 1, 0.85f), 70f, 0.3f);
                log.Push(NameOf(ev.p) + " hit the launcher: nothing!");
                yield return Tw.Wait(0.3f);
                yield break;
            }
            App.Play(Sound.Plus, 1f, 1.2f);
            Tw.Punch(rt, 0.3f, 0.4f);
            Fx.PopSprite(fxLayer, UIKit.S("rays"), rt.position, new Vector2(520, 520), new Color(1f, 0.8f, 0.3f, 0.6f), 0.2f, 90f);
            yield return DrawCardsRoutine(ev.p, ev.n, ev.cards, true);
            log.Push(NameOf(ev.p) + " hit the launcher: <color=#FF5C5C>" + ev.n + " card" + (ev.n == 1 ? "" : "s") + " fired!</color>");
        }

        IEnumerator RouletteRoutine(Ev ev)
        {
            Fx.PopSprite(fxLayer, ArtLibrary.I.Card("w_roulette"), tableCenter.position, new Vector2(240, 336), Color.white, 0.5f, 720f);
            App.Play(Sound.Whoosh);
            yield return Tw.Wait(0.5f);
            yield return DrawCardsRoutine(ev.p, ev.n, ev.cards, true);
            log.Push(NameOf(ev.p) + (ev.p == seat ? " named " : " named ") + Cards.RichColor(ev.color, Table.Side) + " and drew " + ev.n);
        }

        IEnumerator DareResultRoutine(Ev ev)
        {
            bool ok = ev.forced == 0;
            if (ev.p != seat)
            {
                Fx.PopText(fxLayer, ok ? "DARE DONE!" : "NO DARE", oppPanel.Anchor.position, ok ? Palette.Online : new Color(1f, 0.45f, 0.45f), 80f, 0.4f);
                App.Play(ok ? Sound.Found : Sound.Error, 0.7f);
            }
            log.Push(NameOf(ev.p) + (ok ? " completed the dare!" : " took the cards instead"));
            yield return Tw.Wait(0.3f);
        }

        IEnumerator WinRoutine(Ev ev)
        {
            over = true;
            bool won = ev.p == seat;
            mePanel.SetTurn(false);
            oppPanel.SetTurn(false);
            unoButton.gameObject.SetActive(false);
            passButton.gameObject.SetActive(false);
            SetGlowAlpha(deckGlow, 0f);
            foreach (var c in myHand.Cards) { c.SetDim(false); c.Lifted = false; }
            myHand.Layout();
            if (ev.reason == "left") toast.Show(won ? session.Opponent.name + " left the game" : "You left the game");
            else if (ev.reason == "idle") toast.Show(won ? session.Opponent.name + " was inactive" : "You were inactive");
            else if (ev.reason == "stalemate" || ev.reason == "limit") toast.Show("Nobody can play: fewest cards wins");
            App.I.Profile.RecordGame(won, ev.pts);
            Debug.Log("[Game] over: " + (won ? "won" : "lost") + " (" + Table.Mode + ", " + ev.reason + ", " + ev.pts + " pts)");
            yield return Tw.Wait(0.4f);
            App.Play(won ? Sound.Win : Sound.Lose);
            yield return banner.Show(won ? "YOU WON!" : "YOU LOST", won ? Palette.Gold : new Color(0.85f, 0.88f, 0.9f), 1.1f, won);
            result.Show(won, session.Me, session.Opponent, ev.pts, ev.reason, App.I.Profile);
            App.I.Match.EndSession();
        }

        float CanvasScale => fxLayer.lossyScale.x;

        // ---------------------------------------------------------------- state

        void ApplyState(St st, float receivedAt)
        {
            state = st;
            seat = st.you;
            if (Table.Side != st.side)
            {
                Table.Side = st.side;
                if (tableBg != null) tableBg.color = st.side == 1 ? DarkTable : Color.white;
                foreach (var cv in myHand.Cards) cv.Refresh();
                foreach (var cv in discardViews) if (cv != null) cv.Refresh();
                myHand.Resort();
                UpdateModeChip(false);
            }

            ReconcileHand(st.hand);
            ReconcileOpponent(st.counts != null && st.counts.Length > 1 ? st.counts[1 - seat] : oppHand.Cards.Count);
            ShowFlipFaces(st);
            if (discardTop == null || discardTop.Id != st.top) SetTop(st.top);
            SetColor(st.color, false);
            SetArrows(st.dir, false);
            ShowStack(st.pendingKind == "stack" ? st.pending : 0, false);

            if (Table.Mode == "nomercy")
            {
                int oc = st.counts != null && st.counts.Length > 1 ? st.counts[1 - seat] : 0;
                if (oppPanel.countText != null) oppPanel.countText.color = oc >= 20 ? new Color(1f, 0.4f, 0.4f) : Color.white;
                if (!mercyWarned && st.hand.Length >= 20)
                {
                    mercyWarned = true;
                    toast.Show("Careful: 25 cards and you're out!", 2.4f);
                }
            }

            turnLen = Mathf.Max(1f, st.turnMs / 1000f);
            deadline = receivedAt + st.ms / 1000f;
            if (st.over == 1) over = true;
            ShowTurn(st.turn);
            if (MyTurn && lastTurn != seat)
            {
                App.Play(Sound.Turn, 0.8f);
                lastTick = -1;
            }
            lastTurn = st.turn;
            awaiting = false;
            if (pendingPlay >= 0 && System.Array.IndexOf(st.hand, pendingPlay) >= 0) pendingPlay = -1;
            if (st.pendingKind != "roulette") rouletteAsked = false;
            if (st.pendingKind != "dare") dareAsked = false;
            RefreshPlayable();
            HandlePending();
        }

        // UNO Flip: you can see the other side of the opponent's cards and of the draw pile.
        void ShowFlipFaces(St st)
        {
            if (Table.Mode != "flip")
            {
                if (deckTop.sprite != ArtLibrary.I.cardBack && Table.Mode != "attack") deckTop.sprite = ArtLibrary.I.cardBack;
                return;
            }
            int other = 1 - st.side;
            deckTop.sprite = st.pileFace >= 0 ? ArtLibrary.I.FaceSprite(Face.FromCode(st.pileFace), other) : ArtLibrary.I.cardBack;
            if (st.oppFaces == null) return;
            for (int i = 0; i < oppHand.Cards.Count && i < st.oppFaces.Length; i++)
                oppHand.Cards[i].ShowFace(Face.FromCode(st.oppFaces[i]), other);
        }

        void HandlePending()
        {
            if (!MyTurn || leaving) return;
            if (state.pendingKind == "roulette" && !rouletteAsked && !picker.IsOpen)
            {
                rouletteAsked = true;
                picker.Show(c =>
                {
                    awaiting = true;
                    session.Roulette(c < 0 ? 0 : c);
                }, Table.Side, "Color Roulette! Name a colour", false);
            }
            else if (state.pendingKind == "dare" && !dareAsked && !dare.IsOpen)
            {
                dareAsked = true;
                dare.Show(state.dare, Table.Side, ok =>
                {
                    awaiting = true;
                    session.Dare(ok);
                });
            }
        }

        void SetTop(int id)
        {
            var cv = Instantiate(cardPrefab, discardPile);
            cv.Set(id, true);
            cv.SetInteractive(false);
            cv.Rect.anchoredPosition = Vector2.zero;
            cv.Rect.localScale = Vector3.one * DiscardScale;
            cv.Rect.localEulerAngles = new Vector3(0, 0, Random.Range(-10f, 10f));
            discardTop = cv;
            discardViews.Add(cv);
            TrimDiscard();
        }

        void ReconcileHand(int[] ids)
        {
            if (ids == null) return;
            var want = new List<int>(ids);
            bool changed = false;
            for (int i = myHand.Cards.Count - 1; i >= 0; i--)
            {
                var cv = myHand.Cards[i];
                if (want.Remove(cv.Id)) continue;
                myHand.Remove(cv);
                Destroy(cv.gameObject);
                changed = true;
            }
            foreach (int id in want)
            {
                var cv = myHand.Spawn(id, true);
                myHand.Insert(cv);
                changed = true;
            }
            if (changed) myHand.Layout(0.25f);
        }

        void ReconcileOpponent(int n)
        {
            bool changed = false;
            while (oppHand.Cards.Count > n)
            {
                var cv = oppHand.TakeAny();
                Destroy(cv.gameObject);
                changed = true;
            }
            while (oppHand.Cards.Count < n)
            {
                var cv = oppHand.Spawn(-1, false);
                oppHand.Insert(cv);
                changed = true;
            }
            if (changed) oppHand.Layout(0.25f);
            oppPanel.SetCount(n, changed);
        }

        void Rebuild(St st)
        {
            myHand.Clear();
            oppHand.Clear();
            foreach (var cv in discardViews) if (cv != null) Destroy(cv.gameObject);
            discardViews.Clear();
            discardTop = null;
            pendingPlay = -1;
            if (st == null) return;
            Table.Side = st.side;
            if (tableBg != null) tableBg.color = st.side == 1 ? DarkTable : Color.white;
            foreach (int id in st.hand) myHand.Insert(myHand.Spawn(id, true));
            myHand.Layout(0f);
            int oc = st.counts != null && st.counts.Length > 1 ? st.counts[1 - st.you] : 0;
            for (int i = 0; i < oc; i++) oppHand.Insert(oppHand.Spawn(-1, false));
            oppHand.Layout(0f);
            SetTop(st.top);
            UpdateModeChip(false);
        }

        void RefreshPlayable()
        {
            bool mine = MyTurn;
            var legal = mine ? ViewRules.Legal(state) : new List<int>();
            foreach (var c in myHand.Cards)
            {
                bool ok = legal.Contains(c.Id);
                c.SetDim(mine && !ok);
                c.Lifted = mine && ok;
                c.SetGlow(Palette.Gold, mine && state.hasDrawn == 1 && c.Id == state.drawn);
            }
            myHand.Layout(0.2f);

            bool resolving = state.pendingKind == "roulette" || state.pendingKind == "dare";
            passButton.gameObject.SetActive(mine && state.hasDrawn == 1);
            bool showUno = mine && !resolving && myHand.Cards.Count == 2 && legal.Count > 0;
            if (showUno && !unoButton.gameObject.activeSelf)
            {
                unoButton.gameObject.SetActive(true);
                unoButton.transform.localScale = Vector3.zero;
                Tw.Scale(unoButton.transform, 1f, 0.4f, Ease.OutBackStrong);
                // Teach new players the UNO call (the most common way beginners lose cards).
                if (!unoHintShown && App.I.Profile.games < 10)
                {
                    unoHintShown = true;
                    toast.Show("2 cards left: tap  <color=#FFD447>UNO!</color>  before you play", 2.8f);
                }
            }
            else if (!showUno) unoButton.gameObject.SetActive(false);
            if (!showUno) SetUnoArmed(false);

            bool mustDraw = mine && !resolving && state.hasDrawn == 0 && legal.Count == 0;
            Tw.Alpha(deckGlow, mustDraw ? 0.95f : mine && !resolving && state.hasDrawn == 0 ? 0.45f : 0f, 0.25f);
        }

        // ---------------------------------------------------------------- input

        void OnCardClicked(CardView cv)
        {
            if (!CanAct)
            {
                if (!over && !MyTurn && state != null) Tw.Shake(cv.visual, 8f, 0.25f);
                return;
            }
            if (!ViewRules.Legal(state).Contains(cv.Id))
            {
                App.Play(Sound.Error, 0.6f);
                Tw.Shake(cv.visual, 12f, 0.3f);
                if (state.hasDrawn == 1) toast.Show("Play the card you drew, or pass", 1.6f);
                else if (state.pendingKind == "stack") toast.Show(Table.Mode == "nomercy" ? "Stack a draw card of equal or higher value, or draw" : "Stack a +2 or +4, or take the cards", 1.8f);
                return;
            }
            if (Table.FaceOf(cv.Id).IsWild && Table.Mode != "allwild")
            {
                awaiting = true;
                picker.Show(color =>
                {
                    awaiting = false;
                    if (color >= 0) CommitPlay(cv, color);
                }, Table.Side);
                return;
            }
            CommitPlay(cv, -1);
        }

        void CommitPlay(CardView cv, int color)
        {
            if (cv == null || !CanAct) return;
            pendingPlay = cv.Id;
            awaiting = true;
            bool uno = unoArmed;
            SetUnoArmed(false);
            unoButton.gameObject.SetActive(false);
            passButton.gameObject.SetActive(false);
            Tw.Alpha(deckGlow, 0f, 0.15f);
            foreach (var c in myHand.Cards) { c.SetDim(false); c.Lifted = false; }
            myHand.Remove(cv);
            myHand.Layout();
            StartCoroutine(FlyToDiscard(cv, cv.Id));
            if (color >= 0) SetColor(color, true);
            mePanel.SetTurn(false);
            session.Play(cv.Id, color, uno);
        }

        void OnDeckClicked()
        {
            if (!CanAct) return;
            if (state.pendingKind == "roulette" || state.pendingKind == "dare") return;
            if (state.hasDrawn == 1)
            {
                toast.Show("Play the card you drew, or pass", 1.6f);
                return;
            }
            awaiting = true;
            Tw.Punch(Table.Mode == "attack" ? (Transform)launcherImg.transform : drawPile, 0.08f, 0.25f);
            Tw.Alpha(deckGlow, 0f, 0.15f);
            session.Draw();
        }

        void OnPassClicked()
        {
            if (!CanAct || state.hasDrawn != 1) return;
            awaiting = true;
            passButton.gameObject.SetActive(false);
            session.Pass();
        }

        void OnUnoClicked()
        {
            if (!MyTurn) return;
            SetUnoArmed(!unoArmed);
            if (unoArmed)
            {
                App.Play(Sound.Uno, 0.7f);
                Tw.Punch(unoButton.transform, 0.2f, 0.35f);
            }
        }

        void SetUnoArmed(bool on)
        {
            unoArmed = on;
            SetGlowAlpha(unoGlow, on ? 0.9f : 0f);
        }

        void OnEmotePicked(int e)
        {
            App.Play(Sound.Pop);
            mePanel.ShowEmote(e);
            session.Emote(e);
        }

        void OnEmote(int fromSeat, int e)
        {
            if (fromSeat == seat) return;
            App.Play(Sound.Pop);
            oppPanel.ShowEmote(e);
        }

        void OnConnectionChanged(bool lost)
        {
            reconnectBanner.gameObject.SetActive(lost);
            if (!lost) toast.Show("Reconnected", 1.4f);
        }

        void OnLost()
        {
            if (over || leaving) return;
            reconnectBanner.gameObject.SetActive(false);
            dialog.Show("Connection lost", "We couldn't get back into this match.", "HOME", null, GoHome);
        }

        void AskLeave()
        {
            if (over || result.IsOpen) { GoHome(); return; }
            dialog.Show("Leave match?", "If you leave now, you lose this game.", "LEAVE", "STAY", () =>
            {
                leaving = true;
                session.Leave();
                App.I.Match.EndSession();
                App.I.LoadScene(App.HomeScene);
            });
        }

        public void PlayAgain()
        {
            App.I.Match.EndSession();
            App.I.AutoSearch = true;
            App.I.LoadScene(App.HomeScene);
        }

        public void GoHome()
        {
            leaving = true;
            App.I.Match.EndSession();
            App.I.LoadScene(App.HomeScene);
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (picker.IsOpen) picker.Choose(-1);
                else if (emotePanel.IsOpen) emotePanel.Hide();
                else if (menu.IsOpen) menu.Hide();
                else if (dialog.IsOpen) dialog.Close(false);
                else if (!dare.IsOpen) AskLeave();
            }
            if (state == null || over) return;
            float left = deadline - Time.unscaledTime;
            float k = left / turnLen;
            if (state.turn == seat)
            {
                mePanel.SetTimer(k);
                int sec = Mathf.CeilToInt(left);
                if (sec <= 5 && sec >= 1 && sec != lastTick && !busy)
                {
                    lastTick = sec;
                    App.Play(Sound.Tick, 0.7f);
                }
            }
            else oppPanel.SetTimer(k);
        }

        // ---------------------------------------------------------------- autopilot hooks

        public void AutoTapCard(int id)
        {
            var cv = myHand.Find(id);
            if (cv != null) OnCardClicked(cv);
        }

        public void AutoTapDeck() => OnDeckClicked();
        public void AutoTapPass() => OnPassClicked();
        public void AutoTapUno() { if (!unoArmed) OnUnoClicked(); }
        public void AutoPickColor(int c) => picker.Choose(c);
        public void AutoEmote(int e) => OnEmotePicked(e);
        public void AutoDare(bool doDare, bool succeed) => dare.AutoAnswer(doDare, succeed);
    }
}
