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
    /// Drives the table. Messages from the session (online server or local bot game) are queued and
    /// played back one event at a time as animations; after each batch the authoritative state is applied
    /// (hand, counts, top card, turn, timer). Taps on cards are animated straight away (optimistic) and
    /// then confirmed by the server's event.
    /// </summary>
    public sealed partial class GameController : MonoBehaviour
    {
        [Header("Table")]
        public CardView cardPrefab;
        public HandView myHand;
        public HandView oppHand;
        public RectTransform drawPile;
        public Button drawButton;
        public RectTransform discardPile;
        public Image colorGlow;
        public Image deckGlow;
        public RectTransform arrowA, arrowB;
        public RectTransform stackBadge;
        public TextMeshProUGUI stackText;
        public RectTransform fxLayer;
        public RectTransform tableCenter;

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

        public const float DiscardScale = 1.16f;
        public const float DeckScale = 0.9f;

        GameSession session;
        St state;
        int seat;
        bool busy;
        bool awaiting;
        int pendingPlay = -1;
        bool unoArmed;
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

        public bool MyTurn => state != null && !over && state.turn == seat;

        /// <summary>True when the local player may act right now.</summary>
        public bool CanAct => MyTurn && !busy && !awaiting && pendingPlay < 0 && session != null && session.Inbox.Count == 0 &&
                              !picker.IsOpen && !dialog.IsOpen && !leaving;

        void Start()
        {
            session = App.I.Match.Session;
            if (session == null)
            {
                // Scene opened on its own (editor): start a quick offline match.
                App.I.Config.forceOffline = true;
                App.I.Match.StartSearch();
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
                    case "turn": ShowTurn(ev.p, false); break;
                    case "play": yield return PlayRoutine(ev); break;
                    case "uno": yield return UnoRoutine(ev.p); break;
                    case "unoPen": yield return PenaltyRoutine(ev); break;
                    case "reverse": yield return ReverseRoutine(ev.dir); break;
                    case "skip": yield return SkipRoutine(ev.p); break;
                    case "stack": ShowStack(ev.n, true); break;
                    case "draw": yield return DrawRoutine(ev); break;
                    case "reshuffle": yield return ReshuffleRoutine(); break;
                    case "win": yield return WinRoutine(ev); break;
                }
            }
        }

        // ---------------------------------------------------------------- helpers

        string NameOf(int p) => p == seat ? "You" : session.Players[p].name;
        PlayerPanel PanelOf(int p) => p == seat ? mePanel : oppPanel;
        Vector3 DeckWorld => drawPile.position;

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
            var c = Palette.ForCard(color);
            c.a = colorGlow.color.a;
            if (animate)
            {
                Tw.Color(colorGlow, new Color(c.r, c.g, c.b, 0.55f), 0.35f);
                colorGlow.transform.localScale = Vector3.one * 0.8f;
                Tw.Scale(colorGlow.transform, 1f, 0.45f, Ease.OutBack);
            }
            else colorGlow.color = new Color(c.r, c.g, c.b, 0.55f);
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

        void ShowTurn(int p, bool fromState)
        {
            mePanel.SetTurn(!over && p == seat);
            oppPanel.SetTurn(!over && p != seat);
        }

        // ---------------------------------------------------------------- event animations

        IEnumerator DealRoutine(int[] mine, int oppCount)
        {
            App.Play(Sound.Shuffle);
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
            bool first = state == null;
            if (first) log.Push("Game on! First card: " + Cards.RichName(card));
        }

        IEnumerator FlyToDiscard(CardView cv, int id)
        {
            cv.transform.SetParent(discardPile, true);
            cv.transform.SetAsLastSibling();
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
            discardTop = cv;
            discardViews.Add(cv);
            yield return Tw.Wait(dur);
            App.Play(Sound.Play, 0.9f);
            TrimDiscard();
        }

        void TrimDiscard()
        {
            while (discardViews.Count > 5)
            {
                var old = discardViews[0];
                discardViews.RemoveAt(0);
                if (old != null) Destroy(old.gameObject);
            }
        }

        IEnumerator PlayRoutine(Ev ev)
        {
            bool mine = ev.p == seat;
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
            string line = NameOf(ev.p) + " played " + Cards.RichName(ev.card);
            if (Cards.IsWild(ev.card))
            {
                line = NameOf(ev.p) + " played " + Cards.Name(ev.card).ToUpper() + ", picked " + Cards.RichColor(ev.color);
                App.Play(Sound.Whoosh, 0.7f);
                Fx.PopSprite(fxLayer, UIKit.S("glow"), discardPile.position, new Vector2(520, 520), Palette.ForCard(ev.color), 0.2f);
            }
            log.Push(line);
        }

        IEnumerator UnoRoutine(int p)
        {
            App.Play(Sound.Uno);
            var at = PanelOf(p).Anchor.position + new Vector3(0, p == seat ? 160f : -160f, 0) * CanvasScale;
            Fx.PopSprite(fxLayer, UIKit.S("uno_burst"), at, new Vector2(390, 240), Color.white, 0.8f);
            log.Push(NameOf(p) + (p == seat ? " called " : " called ") + "<color=#FFD447>UNO!</color>");
            yield return Tw.Wait(0.5f);
        }

        IEnumerator PenaltyRoutine(Ev ev)
        {
            string who = ev.p == seat ? "You" : NameOf(ev.p);
            toast.Show(who + (ev.p == seat ? " forgot" : " forgot") + " to call UNO!  +" + ev.n);
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
            Fx.PopSprite(fxLayer, UIKit.S("fx_reverse"), tableCenter.position, new Vector2(240, 240), Palette.ForCard(state != null ? state.color : 0), 0.35f, 180f);
            yield return Tw.Wait(0.45f);
        }

        IEnumerator SkipRoutine(int p)
        {
            App.Play(Sound.Whoosh, 0.8f, 1.2f);
            Fx.PopSprite(fxLayer, UIKit.S("fx_skip"), PanelOf(p).Anchor.position, new Vector2(200, 200), Palette.Red, 0.5f);
            yield return Tw.Wait(0.4f);
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
            float gap = n > 4 ? 0.11f : 0.18f;
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
            for (int i = discardViews.Count - 2; i >= 0; i--)
            {
                var cv = discardViews[i];
                if (cv == null) continue;
                cv.transform.SetParent(fxLayer, true);
                Tw.WorldPos(cv.transform, DeckWorld, 0.35f, Ease.InOutCubic, (discardViews.Count - 2 - i) * 0.05f);
                cv.Flip(false, -1, 0.2f);
                Destroy(cv.gameObject, 0.6f);
            }
            if (discardTop != null)
            {
                discardViews.Clear();
                discardViews.Add(discardTop);
            }
            log.Push("<color=#9FB3BA>Draw pile reshuffled</color>");
            yield return Tw.Wait(0.55f);
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
            App.I.Profile.RecordGame(won, ev.pts);
            Debug.Log("[Game] over: " + (won ? "won" : "lost") + " (" + ev.reason + ", " + ev.pts + " pts)");
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

            ReconcileHand(st.hand);
            ReconcileOpponent(st.counts != null && st.counts.Length > 1 ? st.counts[1 - seat] : oppHand.Cards.Count);
            if (discardTop == null || discardTop.Id != st.top) SetTop(st.top);
            SetColor(st.color, false);
            SetArrows(st.dir, false);
            ShowStack(st.pending, false);

            turnLen = Mathf.Max(1f, st.turnMs / 1000f);
            deadline = receivedAt + st.ms / 1000f;
            if (st.over == 1) over = true;
            ShowTurn(st.turn, true);
            if (MyTurn && lastTurn != seat)
            {
                App.Play(Sound.Turn, 0.8f);
                lastTick = -1;
            }
            lastTurn = st.turn;
            awaiting = false;
            if (pendingPlay >= 0 && System.Array.IndexOf(st.hand, pendingPlay) >= 0) pendingPlay = -1;
            RefreshPlayable();
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
            if (changed) myHand.Layout(0.2f);
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
            if (changed) oppHand.Layout(0.2f);
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
            foreach (int id in st.hand) myHand.Insert(myHand.Spawn(id, true));
            myHand.Layout(0f);
            int oc = st.counts != null && st.counts.Length > 1 ? st.counts[1 - st.you] : 0;
            for (int i = 0; i < oc; i++) oppHand.Insert(oppHand.Spawn(-1, false));
            oppHand.Layout(0f);
            SetTop(st.top);
        }

        void RefreshPlayable()
        {
            bool mine = MyTurn;
            var hand = new List<int>();
            foreach (var c in myHand.Cards) hand.Add(c.Id);
            var legal = mine ? ViewRules.Legal(state, hand) : new List<int>();
            foreach (var c in myHand.Cards)
            {
                bool ok = legal.Contains(c.Id);
                c.SetDim(mine && !ok);
                c.Lifted = mine && ok;
                c.SetGlow(Palette.Gold, mine && state.hasDrawn == 1 && c.Id == state.drawn);
            }
            myHand.Layout(0.2f);

            passButton.gameObject.SetActive(mine && state.hasDrawn == 1);
            bool showUno = mine && myHand.Cards.Count == 2 && legal.Count > 0;
            if (showUno && !unoButton.gameObject.activeSelf)
            {
                unoButton.gameObject.SetActive(true);
                unoButton.transform.localScale = Vector3.zero;
                Tw.Scale(unoButton.transform, 1f, 0.4f, Ease.OutBackStrong);
            }
            else if (!showUno) unoButton.gameObject.SetActive(false);
            if (!showUno) SetUnoArmed(false);

            bool mustDraw = mine && state.hasDrawn == 0 && legal.Count == 0;
            Tw.Alpha(deckGlow, mustDraw ? 0.95f : mine && state.hasDrawn == 0 ? 0.45f : 0f, 0.25f);
        }

        // ---------------------------------------------------------------- input

        void OnCardClicked(CardView cv)
        {
            if (!CanAct)
            {
                if (!over && !MyTurn && state != null) Tw.Shake(cv.visual, 8f, 0.25f);
                return;
            }
            var hand = new List<int>();
            foreach (var c in myHand.Cards) hand.Add(c.Id);
            if (!ViewRules.Legal(state, hand).Contains(cv.Id))
            {
                App.Play(Sound.Error, 0.6f);
                Tw.Shake(cv.visual, 12f, 0.3f);
                if (state.hasDrawn == 1) toast.Show("Play the card you drew, or pass", 1.6f);
                return;
            }
            if (Cards.IsWild(cv.Id))
            {
                awaiting = true;
                picker.Show(color =>
                {
                    awaiting = false;
                    if (color >= 0) CommitPlay(cv, color);
                });
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
            if (state.hasDrawn == 1)
            {
                toast.Show("Play the card you drew, or pass", 1.6f);
                return;
            }
            awaiting = true;
            Tw.Punch(drawPile, 0.08f, 0.25f);
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
                else AskLeave();
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
    }
}
