'use strict';
// UNO rules engine (authoritative), all game modes. Mirrored 1:1 in Unity:
// Assets/UnoGame/Scripts/Core/UnoEngine.cs. Any rule change here must be made there too;
// the parity dump (test/parity-dump.js vs the editor's Checks.ParityDump) proves both play identically.
//
// Card ids index the mode's deck (cards.js). Classic ids keep the original mapping:
//   colours 0 red, 1 yellow, 2 green, 3 blue, 4 wild; values 0-9, 10 skip, 11 reverse, 12 draw two,
//   13 wild, 14 wild draw four (see cardOf, kept for the classic tests).

const { K, WILDC, MODES, deckFor, faceCode, isDrawCard, points } = require('./cards');

const SKIP = 10, REVERSE = 11, DRAW2 = 12, WILD = 13, WILD4 = 14;
const HAND_SIZE = 7;
const DECK_SIZE = 108;
const MAX_MOVES = 1000;

// Classic view of a card id ({ c, v }), used by tests and older tooling.
function cardOf(id) {
  if (id >= 100) return { c: 4, v: id < 104 ? WILD : WILD4 };
  const c = Math.floor(id / 25), r = id % 25;
  return { c, v: r === 0 ? 0 : Math.floor((r - 1) / 2) + 1 };
}

function cardPoints(id) {
  const v = cardOf(id).v;
  if (v <= 9) return v;
  if (v <= DRAW2) return 20;
  return 50;
}

// mulberry32: tiny deterministic PRNG, identical in C# (UnoRng).
function makeRng(seed) {
  let a = seed | 0;
  return function () {
    a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

class Game {
  constructor({ players = 2, seed = 1, stacking = true, mode = 'classic' } = {}) {
    this.mode = MODES[mode] ? mode : 'classic';
    this.cfg = MODES[this.mode];
    this.deck = deckFor(this.mode);
    this.n = players;
    this.rng = makeRng(seed);
    this.stacking = this.cfg.stack === 'classic' ? stacking : !!this.cfg.stack;
    this.hands = [];
    for (let i = 0; i < players; i++) this.hands.push([]);
    this.skips = new Array(players).fill(0); // turns each player still has to miss
    this.out = new Array(players).fill(false); // knocked out (No Mercy 25 cards) or left a 3+ player game
    this.drawPile = [];   // top of pile = last element
    this.discard = [];    // top of pile = last element
    this.side = 0;        // UNO Flip: 0 light, 1 dark
    this.color = 0;       // current colour; 4 = anything goes (All Wild, or a wild flipped to the top)
    this.dir = 1;
    this.turn = 0;
    this.pending = 0;     // stacked draw penalty waiting for this.turn
    this.lastDraw = 0;    // value of the last stacked draw card (No Mercy stacks need >=)
    this.pendingKind = ''; // '' | 'stack' | 'roulette' | 'dare': what this.turn must resolve first
    this.dareAmount = 0;
    this.launcherCharge = 0;
    this.idleTurns = 0;   // turns in a row where the deck was empty and nobody could play
    this.moves = 0;       // turns taken; a game that somehow never ends is called at MAX_MOVES
    this.hasDrawn = false;
    this.drawnCard = -1;  // card drawn this turn that may still be played
    this.over = false;
    this.winner = -1;
    this.points = 0;
    this.events = [];
  }

  randInt(n) { return Math.floor(this.rng() * n); }

  shuffle(arr) {
    for (let i = arr.length - 1; i > 0; i--) {
      const j = this.randInt(i + 1);
      const t = arr[i]; arr[i] = arr[j]; arr[j] = t;
    }
  }

  emit(ev) { this.events.push(ev); }
  takeEvents() { const e = this.events; this.events = []; return e; }

  face(id) { const d = this.deck[id]; return this.side === 1 && d.b ? d.b : d.a; }
  otherFace(id) { const d = this.deck[id]; return d.b ? (this.side === 1 ? d.a : d.b) : null; }
  top() { return this.discard[this.discard.length - 1]; }
  // Next player still in the game, in the direction of play.
  next(p) {
    let t = p;
    for (let i = 0; i < this.n; i++) {
      t = (t + this.dir + this.n) % this.n;
      if (!this.out[t]) return t;
    }
    return t;
  }

  activeCount() {
    let c = 0;
    for (let i = 0; i < this.n; i++) if (!this.out[i]) c++;
    return c;
  }

  // 3+ players: Swap / Trade Hands, the No Mercy 7 and Targeted Draw let the player pick who.
  needsTarget(f) {
    if (this.activeCount() <= 2) return false;
    return f.k === K.SWAP || f.k === K.TARGET_DRAW || (this.cfg.sevenZero && f.k === K.NUM && f.n === 7);
  }

  validTarget(p, t) {
    return Number.isInteger(t) && t >= 0 && t < this.n && t !== p && !this.out[t];
  }

  // A card with no effect, so the game can start on it.
  isPlain(f) { return f.k === K.NUM || (this.cfg.allWild && f.k === K.WILD); }

  start(firstPlayer) {
    const ids = [];
    for (let i = 0; i < this.deck.length; i++) ids.push(i);
    this.shuffle(ids);
    this.drawPile = ids;
    for (let r = 0; r < HAND_SIZE; r++)
      for (let p = 0; p < this.n; p++) this.hands[p].push(this.drawPile.pop());
    for (let p = 0; p < this.n; p++) this.emit({ k: 'deal', p, n: HAND_SIZE, cards: this.hands[p].slice() });

    // The first discard is always a card without an effect: others go back into the pile.
    let first = this.drawPile.pop();
    while (!this.isPlain(this.face(first))) {
      this.drawPile.splice(this.randInt(this.drawPile.length), 0, first);
      first = this.drawPile.pop();
    }
    this.discard.push(first);
    this.color = this.face(first).c;
    this.emit({ k: 'start', card: first, color: this.color });

    this.turn = (firstPlayer === undefined || firstPlayer < 0) ? this.randInt(this.n) : firstPlayer;
    this.emit({ k: 'turn', p: this.turn });
  }

  sameSymbol(f, t) {
    if (f.k !== t.k) return false;
    return (f.k === K.NUM || f.k === K.DRAW || f.k === K.WILD_DRAW || f.k === K.HIT) ? f.n === t.n : true;
  }

  isPlayable(p, id) {
    const f = this.face(id);
    if (this.pendingKind === 'stack') {
      if (!isDrawCard(f)) return false;
      return this.cfg.stack === 'nomercy' ? f.n >= this.lastDraw : true;
    }
    if (this.pendingKind) return false;
    if (this.cfg.allWild) return true;
    if (f.c === WILDC) {
      if (this.cfg.restrictWildDraw && (f.k === K.WILD_DRAW || f.k === K.WILD_DRAW_COLOR)) {
        // Official rule: only when you hold no other card of the current colour.
        const hand = this.hands[p];
        for (let i = 0; i < hand.length; i++)
          if (hand[i] !== id && this.face(hand[i]).c === this.color) return false;
      }
      return true;
    }
    if (this.color === WILDC) return true;
    return f.c === this.color || this.sameSymbol(f, this.face(this.top()));
  }

  legalMoves(p) {
    if (this.over || p !== this.turn) return [];
    if (this.hasDrawn) return this.drawnCard >= 0 && this.isPlayable(p, this.drawnCard) ? [this.drawnCard] : [];
    return this.hands[p].filter(id => this.isPlayable(p, id));
  }

  drawCards(p, k) {
    const got = [];
    for (let i = 0; i < k; i++) {
      const id = this.drawOne();
      if (id < 0) break;
      this.hands[p].push(id);
      got.push(id);
    }
    return got;
  }

  drawOne() {
    if (this.drawPile.length === 0) this.reshuffle();
    if (this.drawPile.length === 0) return -1;
    return this.drawPile.pop();
  }

  // Draw until a card matching `test` comes up (it is kept too). Capped so an empty deck can't loop.
  drawUntil(p, test) {
    const got = [];
    for (let guard = 0; guard < 60; guard++) {
      const id = this.drawOne();
      if (id < 0) break;
      this.hands[p].push(id);
      got.push(id);
      if (test(id)) break;
    }
    return got;
  }

  reshuffle() {
    if (this.discard.length <= 1) return;
    const top = this.discard.pop();
    const rest = this.discard;
    this.discard = [top];
    this.shuffle(rest);
    this.drawPile = rest;
    this.emit({ k: 'reshuffle', n: rest.length });
  }

  // UNO Attack: each hit either fires a burst of cards or nothing; it gets likelier to fire every miss.
  hitLauncher(p) {
    const chance = Math.min(0.85, 0.22 + 0.16 * this.launcherCharge);
    let got = [];
    if (this.rng() < chance) {
      const count = 1 + this.randInt(4) + (this.randInt(3) === 0 ? 2 : 0);
      this.launcherCharge = 0;
      got = this.drawCards(p, count);
    } else this.launcherCharge++;
    this.emit({ k: 'launch', p, n: got.length, cards: got });
    return got;
  }

  swapHands(a, b) {
    const t = this.hands[a];
    this.hands[a] = this.hands[b];
    this.hands[b] = t;
    this.emit({ k: 'swap', p: a, n: b });
  }

  // No Mercy 0 with 3+ players: everyone passes their hand on in the direction of play.
  rotateHands(p) {
    const order = [p];
    for (let t = this.next(p); t !== p && order.length < this.n; t = this.next(t)) order.push(t);
    const moved = order.map(s => this.hands[s]);
    for (let i = 0; i < order.length; i++) this.hands[order[(i + 1) % order.length]] = moved[i];
    this.emit({ k: 'rotate', p, dir: this.dir });
  }

  // A player leaves a game of 3+: their cards go to the bottom of the draw pile and play goes on.
  // Returns true when only one player is left (who then wins).
  eliminate(p, reason) {
    this.out[p] = true;
    this.skips[p] = 0;
    const cards = this.hands[p];
    this.hands[p] = [];
    this.shuffle(cards);
    this.drawPile.unshift(...cards);
    this.emit({ k: 'out', p, n: cards.length, reason });
    if (this.activeCount() > 1) return false;
    this.finish(this.next(p), reason);
    return true;
  }

  // No Mercy: a hand of 25+ cards is out. Returns true when that ended the game.
  checkMercy() {
    if (!this.cfg.mercy || this.over) return false;
    for (let i = 0; i < this.n; i++) {
      if (this.out[i] || this.hands[i].length < this.cfg.mercy) continue;
      this.emit({ k: 'mercy', p: i, n: this.hands[i].length });
      if (this.activeCount() <= 2) {
        this.finish(this.next(i), 'mercy');
        return true;
      }
      if (this.eliminate(i, 'mercy')) return true;
    }
    return false;
  }

  // Every card is in someone's hand and nobody can play: the fewest cards (then fewest points) wins.
  // Returns true when that ended the game.
  checkStalemate(idle) {
    this.idleTurns = idle ? this.idleTurns + 1 : 0;
    if (this.idleTurns < this.activeCount()) return false;
    this.finishByCount('stalemate');
    return true;
  }

  finishByCount(reason) {
    let best = this.turn, bestCount = Infinity, bestPts = Infinity;
    for (let i = 0; i < this.n; i++) {
      const p = (this.turn + i) % this.n;
      if (this.out[p]) continue;
      const count = this.hands[p].length;
      let pts = 0;
      for (const id of this.hands[p]) pts += points(this.mode, this.face(id), this.side);
      if (count < bestCount || (count === bestCount && pts < bestPts)) { best = p; bestCount = count; bestPts = pts; }
    }
    this.finish(best, reason);
  }

  endTurn(p) {
    if (++this.moves >= MAX_MOVES) { this.finishByCount('limit'); return; }
    let t = this.next(p);
    for (let guard = 0; this.skips[t] > 0 && guard < 50; guard++) {
      this.skips[t]--;
      t = this.next(t);
    }
    this.turn = t;
    this.hasDrawn = false;
    this.drawnCard = -1;
    this.emit({ k: 'turn', p: this.turn });
  }

  forcedDraw(t, amount) {
    const got = this.drawCards(t, amount);
    this.emit({ k: 'draw', p: t, n: got.length, cards: got, forced: 1 });
    return got;
  }

  flipDeck() {
    this.side ^= 1;
    this.discard.reverse();
    this.drawPile.reverse();
    const top = this.face(this.top());
    this.color = top.c;
    this.emit({ k: 'flip', n: this.side, card: this.top(), color: this.color });
  }

  // forgive: the player forgot UNO but nobody caught it (bot opponents don't always notice).
  // target: who a Swap / Trade Hands / No Mercy 7 / Targeted Draw goes to (3+ players; defaults to the next player).
  play(p, id, chosen, uno, forgive, target) {
    if (this.over) return 'game over';
    if (p !== this.turn) return 'not your turn';
    const hand = this.hands[p];
    const idx = hand.indexOf(id);
    if (idx < 0) return 'card not in hand';
    if (this.hasDrawn && id !== this.drawnCard) return 'only the drawn card can be played';
    if (!this.isPlayable(p, id)) return 'card cannot be played';
    const f = this.face(id);
    const needsColor = f.c === WILDC && !this.cfg.allWild;
    if (needsColor && !(chosen >= 0 && chosen <= 3)) return 'choose a colour';

    hand.splice(idx, 1);
    this.discard.push(id);
    this.idleTurns = 0;
    this.color = f.c === WILDC ? (this.cfg.allWild ? WILDC : chosen) : f.c;
    this.hasDrawn = false;
    this.drawnCard = -1;
    this.emit({ k: 'play', p, card: id, color: this.color });

    if (f.k === K.DISCARD_ALL) {
      const extra = hand.filter(h => this.face(h).c === f.c);
      for (const h of extra) hand.splice(hand.indexOf(h), 1);
      this.discard.splice(this.discard.length - 1, 0, ...extra);
      this.emit({ k: 'discardAll', p, n: extra.length, cards: extra.slice() });
    }

    const swaps = f.k === K.SWAP || (this.cfg.sevenZero && f.k === K.NUM && (f.n === 7 || f.n === 0));
    if (hand.length === 1 && !swaps) {
      if (uno) this.emit({ k: 'uno', p });
      else if (!forgive) {
        const got = this.drawCards(p, 2);
        this.emit({ k: 'unoPen', p, n: got.length, cards: got });
      }
    }

    const t = this.next(p);
    const tgt = this.needsTarget(f) && this.validTarget(p, target) ? target : t;
    switch (f.k) {
      case K.SKIP:
        this.emit({ k: 'skip', p: t });
        this.skips[t]++;
        break;
      case K.SKIP_ALL:
        for (let i = t, guard = 0; i !== p && guard < this.n; i = this.next(i), guard++) {
          this.emit({ k: 'skip', p: i });
          this.skips[i]++;
        }
        break;
      case K.SKIP_TWO:
        this.emit({ k: 'skip', p: t, n: 2 });
        this.skips[t] += 2;
        break;
      case K.REVERSE:
        this.dir = -this.dir;
        this.emit({ k: 'reverse', dir: this.dir });
        if (this.n === 2) this.skips[t]++;
        break;
      case K.DRAW:
      case K.WILD_DRAW:
      case K.WILD_REV_DRAW:
        if (f.k === K.WILD_REV_DRAW) {
          this.dir = -this.dir;
          this.emit({ k: 'reverse', dir: this.dir });
        }
        if (this.stacking) {
          this.pending += f.n;
          this.lastDraw = f.n;
          this.pendingKind = 'stack';
          this.emit({ k: 'stack', n: this.pending });
        } else {
          this.forcedDraw(this.next(p), f.n);
          this.skips[this.next(p)]++;
        }
        break;
      case K.WILD_DRAW_COLOR: {
        const want = this.color;
        const got = this.drawUntil(t, h => this.face(h).c === want);
        this.emit({ k: 'draw', p: t, n: got.length, cards: got, forced: 1 });
        this.skips[t]++;
        break;
      }
      case K.ROULETTE:
        this.pendingKind = 'roulette';
        this.emit({ k: 'roulettePending', p: t });
        break;
      case K.FLIP:
        this.flipDeck();
        break;
      case K.TARGET_DRAW:
        this.forcedDraw(tgt, f.n);
        break;
      case K.SWAP:
        this.swapHands(p, tgt);
        break;
      case K.NUM:
        if (swaps && f.n === 0 && this.activeCount() > 2) this.rotateHands(p);
        else if (swaps) this.swapHands(p, tgt);
        break;
      case K.HIT:
        for (let i = 0; i < f.n && !this.over; i++) this.hitLauncher(t);
        this.skips[t]++;
        break;
      case K.HIT_ALL:
        for (let i = 0; i < this.n; i++) if (i !== p && !this.out[i]) this.hitLauncher(i);
        break;
      case K.HIT_FIRE:
        for (let guard = 0; guard < 25; guard++) if (this.hitLauncher(t).length > 0 || this.drawPile.length + this.discard.length <= 1) break;
        this.skips[t]++;
        break;
      case K.DARE:
      case K.WILD_DARE:
        this.pendingKind = 'dare';
        this.dareAmount = f.n;
        this.emit({ k: 'darePending', p: t, n: f.n });
        break;
    }

    if (this.checkMercy()) return null;

    // Someone with no cards left wins (a swap can hand the win to the other player).
    for (let i = 0; i < this.n; i++) {
      const w = (p + i) % this.n;
      if (this.out[w] || this.hands[w].length !== 0) continue;
      if (this.pendingKind === 'stack' && this.pending > 0) {
        // A final draw card still makes the next player draw; those cards count for points.
        const victim = this.next(w);
        this.forcedDraw(victim, this.pending);
        this.pending = 0;
        this.pendingKind = '';
      }
      this.pendingKind = '';
      this.finish(w, 'cards');
      return null;
    }

    this.endTurn(p);
    return null;
  }

  draw(p) {
    if (this.over) return 'game over';
    if (p !== this.turn) return 'not your turn';
    if (this.pendingKind === 'stack') {
      const amount = this.pending;
      this.pending = 0;
      this.lastDraw = 0;
      this.pendingKind = '';
      this.forcedDraw(p, amount);
      if (this.checkMercy()) return null;
      this.endTurn(p);
      return null;
    }
    if (this.pendingKind === 'roulette') return 'choose a colour for the roulette';
    if (this.pendingKind === 'dare') return 'do the dare or draw';
    if (this.hasDrawn) return 'already drew this turn';

    const deckEmpty = this.drawPile.length === 0 && this.discard.length <= 1;
    if (this.cfg.launcher) {
      const fired = this.hitLauncher(p);
      if (this.checkStalemate(deckEmpty && fired.length === 0)) return null;
      this.endTurn(p);
      return null;
    }

    let got;
    if (this.cfg.drawUntil) got = this.drawUntil(p, h => this.isPlayable(p, h));
    else got = this.drawCards(p, 1);
    this.emit({ k: 'draw', p, n: got.length, cards: got, forced: 0 });
    if (this.checkMercy()) return null;
    if (this.out[p]) { this.endTurn(p); return null; }
    if (this.checkStalemate(deckEmpty && got.length === 0)) return null;
    const last = got.length ? got[got.length - 1] : -1;
    if (last >= 0 && this.isPlayable(p, last)) {
      this.hasDrawn = true;
      this.drawnCard = last;
      return null; // still this player's turn: play the drawn card or pass
    }
    this.endTurn(p);
    return null;
  }

  pass(p) {
    if (this.over) return 'game over';
    if (p !== this.turn) return 'not your turn';
    if (!this.hasDrawn) return 'draw a card first';
    this.endTurn(p);
    return null;
  }

  // No Mercy Color Roulette: the victim names a colour and draws until it comes up.
  roulette(p, color) {
    if (this.over) return 'game over';
    if (p !== this.turn || this.pendingKind !== 'roulette') return 'no roulette to resolve';
    if (!(color >= 0 && color <= 3)) return 'choose a colour';
    this.pendingKind = '';
    const got = this.drawUntil(p, h => this.face(h).c === color);
    this.emit({ k: 'roulette', p, color, n: got.length, cards: got });
    if (this.checkMercy()) return null;
    this.endTurn(p);
    return null;
  }

  // UNO Dare: ok = the dare was done (keep your turn), otherwise draw the penalty and lose the turn.
  dare(p, ok) {
    if (this.over) return 'game over';
    if (p !== this.turn || this.pendingKind !== 'dare') return 'no dare to resolve';
    const amount = this.dareAmount;
    this.pendingKind = '';
    this.dareAmount = 0;
    this.emit({ k: 'dare', p, n: amount, forced: ok ? 0 : 1 });
    if (ok) {
      this.emit({ k: 'turn', p });
      return null;
    }
    this.forcedDraw(p, amount);
    this.endTurn(p);
    return null;
  }

  // What the server does when a player's turn timer runs out.
  autoMove(p) {
    if (this.over || p !== this.turn) return;
    if (this.pendingKind === 'stack') { this.draw(p); return; }
    if (this.pendingKind === 'roulette') { this.roulette(p, this.randInt(4)); return; }
    if (this.pendingKind === 'dare') { this.dare(p, false); return; }
    if (!this.hasDrawn) this.draw(p);
    if (!this.over && this.turn === p && this.hasDrawn) this.pass(p);
  }

  finish(winner, reason) {
    this.over = true;
    this.winner = winner;
    let pts = 0;
    for (let i = 0; i < this.n; i++)
      if (i !== winner) for (const id of this.hands[i]) pts += points(this.mode, this.face(id), this.side);
    this.points = pts;
    this.hasDrawn = false;
    this.drawnCard = -1;
    this.emit({ k: 'win', p: winner, pts, reason: reason || 'cards' });
  }

  // A player leaves or idles out. Two players left: the other one wins. More: the game goes on without them.
  forfeit(loser, reason) {
    if (this.over || this.out[loser]) return;
    reason = reason || 'forfeit';
    if (this.activeCount() <= 2) {
      let w = (loser + 1) % this.n;
      for (let g = 0; g < this.n && (w === loser || this.out[w]); g++) w = (w + 1) % this.n;
      this.finish(w, reason);
      return;
    }
    const wasTurn = this.turn === loser;
    if (this.eliminate(loser, reason)) return;
    if (wasTurn) {
      this.pending = 0;
      this.lastDraw = 0;
      this.pendingKind = '';
      this.dareAmount = 0;
      this.endTurn(loser);
    }
  }

  // UNO Flip: the other side of every opponent's cards, seat by seat from the one after you.
  othersFaces(seat) {
    const r = [];
    for (let k = 1; k < this.n; k++) for (const id of this.hands[(seat + k) % this.n]) r.push(faceCode(this.otherFace(id)));
    return r;
  }

  view(seat) {
    const mine = this.turn === seat;
    const flip = !!this.cfg.flip;
    return {
      you: seat,
      mode: this.mode,
      side: this.side,
      hand: this.hands[seat].slice(),
      counts: this.hands.map(h => h.length),
      out: this.out.map(o => (o ? 1 : 0)),
      top: this.top(),
      color: this.color,
      dir: this.dir,
      turn: this.turn,
      pending: this.pending,
      lastDraw: this.lastDraw,
      pendingKind: this.pendingKind,
      dare: this.dareAmount,
      hasDrawn: mine && this.hasDrawn ? 1 : 0,
      drawn: mine ? this.drawnCard : -1,
      legal: mine ? this.legalMoves(seat) : [],
      pile: this.drawPile.length,
      // UNO Flip: you can see the other side of your opponents' cards and of the top of the draw pile.
      oppFaces: flip ? this.othersFaces(seat) : [],
      pileFace: flip && this.drawPile.length ? faceCode(this.otherFace(this.drawPile[this.drawPile.length - 1])) : -1,
      over: this.over ? 1 : 0,
      winner: this.winner,
      pts: this.points,
    };
  }

  // Events as seen by one seat: other players' drawn cards are hidden.
  eventsFor(seat, events) {
    return events.map(ev => (ev.cards && ev.p !== seat && ev.k !== 'discardAll') ? Object.assign({}, ev, { cards: [] }) : ev);
  }
}

module.exports = { Game, cardOf, cardPoints, makeRng, SKIP, REVERSE, DRAW2, WILD, WILD4, HAND_SIZE, DECK_SIZE, K };
