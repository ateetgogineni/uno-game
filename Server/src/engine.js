'use strict';
// UNO rules engine (authoritative). Mirrored 1:1 in Unity: Assets/UnoGame/Scripts/Core/UnoEngine.cs.
// Any rule change here must be made there too; test/parity.test.js checks both produce identical games.
//
// Card ids 0..107 map to a fixed colour/value (see cardOf):
//   colours 0 red, 1 yellow, 2 green, 3 blue, 4 wild
//   values  0-9 numbers, 10 skip, 11 reverse, 12 draw two, 13 wild, 14 wild draw four
// Official deck: per colour one 0 and two each of 1-9, Skip, Reverse, Draw Two; 4 Wild; 4 Wild Draw Four.

const SKIP = 10, REVERSE = 11, DRAW2 = 12, WILD = 13, WILD4 = 14;
const HAND_SIZE = 7;
const DECK_SIZE = 108;

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
  constructor({ players = 2, seed = 1, stacking = true } = {}) {
    this.n = players;
    this.rng = makeRng(seed);
    this.stacking = stacking;
    this.hands = [];
    for (let i = 0; i < players; i++) this.hands.push([]);
    this.drawPile = [];   // top of pile = last element
    this.discard = [];    // top of pile = last element
    this.color = 0;
    this.dir = 1;
    this.turn = 0;
    this.pending = 0;     // stacked draw penalty waiting for this.turn
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

  top() { return this.discard[this.discard.length - 1]; }
  next(p) { return (p + this.dir + this.n) % this.n; }

  start(firstPlayer) {
    const ids = [];
    for (let i = 0; i < DECK_SIZE; i++) ids.push(i);
    this.shuffle(ids);
    this.drawPile = ids;
    for (let r = 0; r < HAND_SIZE; r++)
      for (let p = 0; p < this.n; p++) this.hands[p].push(this.drawPile.pop());
    for (let p = 0; p < this.n; p++) this.emit({ k: 'deal', p, n: HAND_SIZE, cards: this.hands[p].slice() });

    // The first discard is always a number card: action and wild cards go back into the pile.
    let first = this.drawPile.pop();
    while (cardOf(first).v > 9) {
      this.drawPile.splice(this.randInt(this.drawPile.length), 0, first);
      first = this.drawPile.pop();
    }
    this.discard.push(first);
    this.color = cardOf(first).c;
    this.emit({ k: 'start', card: first, color: this.color });

    this.turn = (firstPlayer === undefined || firstPlayer < 0) ? this.randInt(this.n) : firstPlayer;
    this.emit({ k: 'turn', p: this.turn });
  }

  isPlayable(p, id) {
    const card = cardOf(id);
    if (this.pending > 0) return card.v === DRAW2 || card.v === WILD4;
    if (card.v === WILD) return true;
    if (card.v === WILD4) {
      // Official rule: only when you hold no other card of the current colour.
      const hand = this.hands[p];
      for (let i = 0; i < hand.length; i++)
        if (hand[i] !== id && cardOf(hand[i]).c === this.color) return false;
      return true;
    }
    return card.c === this.color || card.v === cardOf(this.top()).v;
  }

  legalMoves(p) {
    if (this.over || p !== this.turn) return [];
    if (this.hasDrawn) return this.drawnCard >= 0 && this.isPlayable(p, this.drawnCard) ? [this.drawnCard] : [];
    return this.hands[p].filter(id => this.isPlayable(p, id));
  }

  drawCards(p, k) {
    const got = [];
    for (let i = 0; i < k; i++) {
      if (this.drawPile.length === 0) this.reshuffle();
      if (this.drawPile.length === 0) break;
      const id = this.drawPile.pop();
      this.hands[p].push(id);
      got.push(id);
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

  // forgive: the player forgot UNO but nobody caught it (bot opponents don't always notice).
  play(p, id, chosen, uno, forgive) {
    if (this.over) return 'game over';
    if (p !== this.turn) return 'not your turn';
    const hand = this.hands[p];
    const idx = hand.indexOf(id);
    if (idx < 0) return 'card not in hand';
    if (this.hasDrawn && id !== this.drawnCard) return 'only the drawn card can be played';
    if (!this.isPlayable(p, id)) return 'card cannot be played';
    const card = cardOf(id);
    if (card.c === 4 && !(chosen >= 0 && chosen <= 3)) return 'choose a colour';

    hand.splice(idx, 1);
    this.discard.push(id);
    this.color = card.c === 4 ? chosen : card.c;
    this.hasDrawn = false;
    this.drawnCard = -1;
    this.emit({ k: 'play', p, card: id, color: this.color });

    if (hand.length === 1) {
      if (uno) this.emit({ k: 'uno', p });
      else if (!forgive) {
        const got = this.drawCards(p, 2);
        this.emit({ k: 'unoPen', p, n: got.length, cards: got });
      }
    }

    let again = false; // 2-player Skip/Reverse: the same player goes again
    if (card.v === REVERSE) {
      this.dir = -this.dir;
      this.emit({ k: 'reverse', dir: this.dir });
      if (this.n === 2) again = true;
    } else if (card.v === SKIP) {
      this.emit({ k: 'skip', p: this.next(p) });
      again = true;
    } else if (card.v === DRAW2 || card.v === WILD4) {
      const amount = card.v === DRAW2 ? 2 : 4;
      if (this.stacking) {
        this.pending += amount;
        this.emit({ k: 'stack', n: this.pending });
      } else {
        const t = this.next(p);
        const got = this.drawCards(t, amount);
        this.emit({ k: 'draw', p: t, n: got.length, cards: got, forced: 1 });
        again = true;
      }
    }

    if (hand.length === 0) {
      if (this.pending > 0) {
        // A final Draw Two / Wild Draw Four still makes the next player draw; those cards count for points.
        const t = this.next(p);
        const got = this.drawCards(t, this.pending);
        this.pending = 0;
        this.emit({ k: 'draw', p: t, n: got.length, cards: got, forced: 1 });
      }
      this.finish(p);
      return null;
    }

    this.turn = again ? (this.n === 2 ? p : this.next(this.next(p))) : this.next(p);
    this.emit({ k: 'turn', p: this.turn });
    return null;
  }

  draw(p) {
    if (this.over) return 'game over';
    if (p !== this.turn) return 'not your turn';
    if (this.pending > 0) {
      const amount = this.pending;
      this.pending = 0;
      const got = this.drawCards(p, amount);
      this.emit({ k: 'draw', p, n: got.length, cards: got, forced: 1 });
      this.turn = this.next(p);
      this.emit({ k: 'turn', p: this.turn });
      return null;
    }
    if (this.hasDrawn) return 'already drew this turn';
    const got = this.drawCards(p, 1);
    this.emit({ k: 'draw', p, n: got.length, cards: got, forced: 0 });
    if (got.length === 1 && this.isPlayable(p, got[0])) {
      this.hasDrawn = true;
      this.drawnCard = got[0];
      return null; // still this player's turn: play the drawn card or pass
    }
    this.turn = this.next(p);
    this.emit({ k: 'turn', p: this.turn });
    return null;
  }

  pass(p) {
    if (this.over) return 'game over';
    if (p !== this.turn) return 'not your turn';
    if (!this.hasDrawn) return 'draw a card first';
    this.hasDrawn = false;
    this.drawnCard = -1;
    this.turn = this.next(p);
    this.emit({ k: 'turn', p: this.turn });
    return null;
  }

  // What the server does when a player's turn timer runs out.
  autoMove(p) {
    if (this.over || p !== this.turn) return;
    if (this.pending > 0) { this.draw(p); return; }
    if (!this.hasDrawn) this.draw(p);
    if (!this.over && this.turn === p && this.hasDrawn) this.pass(p);
  }

  finish(winner, reason) {
    this.over = true;
    this.winner = winner;
    let pts = 0;
    for (let i = 0; i < this.n; i++)
      if (i !== winner) for (const id of this.hands[i]) pts += cardPoints(id);
    this.points = pts;
    this.hasDrawn = false;
    this.drawnCard = -1;
    this.emit({ k: 'win', p: winner, pts, reason: reason || 'cards' });
  }

  forfeit(loser, reason) {
    if (this.over) return;
    this.finish((loser + 1) % this.n, reason || 'forfeit');
  }

  view(seat) {
    const mine = this.turn === seat;
    return {
      you: seat,
      hand: this.hands[seat].slice(),
      counts: this.hands.map(h => h.length),
      top: this.top(),
      color: this.color,
      dir: this.dir,
      turn: this.turn,
      pending: this.pending,
      hasDrawn: mine && this.hasDrawn ? 1 : 0,
      drawn: mine ? this.drawnCard : -1,
      pile: this.drawPile.length,
      over: this.over ? 1 : 0,
      winner: this.winner,
      pts: this.points,
    };
  }

  // Events as seen by one seat: other players' drawn cards are hidden.
  eventsFor(seat, events) {
    return events.map(ev => (ev.cards && ev.p !== seat) ? Object.assign({}, ev, { cards: [] }) : ev);
  }
}

module.exports = { Game, cardOf, cardPoints, makeRng, SKIP, REVERSE, DRAW2, WILD, WILD4, HAND_SIZE, DECK_SIZE };
