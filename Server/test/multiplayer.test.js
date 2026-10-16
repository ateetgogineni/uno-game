'use strict';
// Games of 3 to 10 players: turn order, Skip Everyone, Reverse, targeted cards, hand rotation,
// the No Mercy knockout, players leaving, and bot games at every table size in every mode.
const test = require('node:test');
const assert = require('node:assert');
const { Game } = require('../src/engine');
const { K, MODE_IDS, deckFor } = require('../src/cards');
const { chooseMove, applyMove, fewestCards } = require('../src/bot');

function conservation(g) {
  const all = [...g.drawPile, ...g.discard, ...g.hands.flat()];
  assert.strictEqual(all.length, g.deck.length, 'card count');
  assert.strictEqual(new Set(all).size, g.deck.length, 'duplicate card');
}

// Ids of cards in the mode's deck whose light face matches, in deck order.
function cards(mode, pred, count = 1, skip = new Set()) {
  const deck = deckFor(mode);
  const out = [];
  for (let i = 0; i < deck.length && out.length < count; i++) if (!skip.has(i) && pred(deck[i].a)) { out.push(i); skip.add(i); }
  if (out.length < count) throw new Error('not enough cards');
  return out;
}

// A started n-player game with chosen hands and top card; every other card is in the draw pile.
function scenario(mode, hands, top, color) {
  const g = new Game({ players: hands.length, seed: 5, mode });
  const used = new Set([top, ...hands.flat()]);
  g.hands = hands.map(h => h.slice());
  g.discard = [top];
  g.drawPile = [];
  for (let i = 0; i < g.deck.length; i++) if (!used.has(i)) g.drawPile.push(i);
  g.color = color === undefined ? g.face(top).c : color;
  g.turn = 0;
  return g;
}

const num = (c, n) => f => f.k === K.NUM && f.c === c && f.n === n;

test('turn order goes round the table and Reverse turns it back without skipping anyone', () => {
  const used = new Set();
  const [r3, r5, rev, r6] = [...cards('classic', num(0, 3), 1, used), ...cards('classic', num(0, 5), 1, used),
    ...cards('classic', f => f.k === K.REVERSE && f.c === 0, 1, used), ...cards('classic', num(0, 6), 1, used)];
  const filler = cards('classic', f => f.k === K.NUM && f.c === 1, 8, used);
  const g = scenario('classic', [[r5, filler[0], filler[1]], [rev, filler[2], filler[3]], [r6, filler[4], filler[5]]], r3);
  assert.strictEqual(g.play(0, r5, -1, false), null);
  assert.strictEqual(g.turn, 1);
  assert.strictEqual(g.play(1, rev, -1, false), null);
  assert.strictEqual(g.dir, -1);
  assert.strictEqual(g.turn, 0, 'with three players Reverse is not a skip');
  conservation(g);
});

test('Skip Everyone skips every opponent at a big table', () => {
  const used = new Set();
  const [top] = cards('nomercy', num(0, 4), 1, used);
  const [skipAll] = cards('nomercy', f => f.k === K.SKIP_ALL && f.c === 0, 1, used);
  const filler = cards('nomercy', f => f.k === K.NUM && f.c === 2, 12, used);
  const g = scenario('nomercy', [[skipAll, filler[0], filler[1]], filler.slice(2, 5), filler.slice(5, 8), filler.slice(8, 11)], top);
  g.play(0, skipAll, -1, false);
  assert.strictEqual(g.turn, 0, 'everyone else is skipped');
  assert.deepStrictEqual(g.takeEvents().filter(e => e.k === 'skip').map(e => e.p), [1, 2, 3]);
});

test('No Mercy 7 swaps with the chosen player and 0 passes every hand on', () => {
  const used = new Set();
  const [top] = cards('nomercy', num(0, 4), 1, used);
  const [seven, zero] = [...cards('nomercy', num(0, 7), 1, used), ...cards('nomercy', num(0, 0), 1, used)];
  const f = cards('nomercy', f => f.k === K.NUM && f.c === 3 && f.n >= 1 && f.n <= 6, 12, used);
  const hands = [[seven, zero, f[0]], [f[1], f[2]], [f[3], f[4], f[5], f[6]], [f[7], f[8], f[9]]];
  const g = scenario('nomercy', hands, top);
  assert.strictEqual(g.play(0, seven, -1, false, false, 2), null);
  assert.deepStrictEqual(g.hands[0], hands[2], 'took player 2\'s hand');
  assert.deepStrictEqual(g.hands[2], [zero, f[0]], 'player 2 got the rest of mine');
  assert.strictEqual(g.turn, 1);
  conservation(g);

  // 0: every hand moves one seat on in the direction of play.
  const g2 = scenario('nomercy', hands, top);
  g2.play(0, zero, -1, false);
  assert.deepStrictEqual(g2.hands[1], [seven, f[0]]);
  assert.deepStrictEqual(g2.hands[2], hands[1]);
  assert.deepStrictEqual(g2.hands[3], hands[2]);
  assert.deepStrictEqual(g2.hands[0], hands[3]);
  assert.ok(g2.takeEvents().some(e => e.k === 'rotate'));
  conservation(g2);
});

test('Targeted Draw hits the chosen player; a bad target falls back to the next player', () => {
  const used = new Set();
  const [top] = cards('allwild', f => f.k === K.WILD, 1, used);
  const [td1, td2] = cards('allwild', f => f.k === K.TARGET_DRAW, 2, used);
  const filler = cards('allwild', f => f.k === K.WILD, 9, used);
  const g = scenario('allwild', [[td1, td2, filler[0]], filler.slice(1, 3), filler.slice(3, 5), filler.slice(5, 7)], top);
  g.play(0, td1, -1, false, false, 3);
  assert.deepStrictEqual(g.hands.map(h => h.length), [2, 2, 2, 4]);
  g.turn = 0;
  g.play(0, td2, -1, true, false, 0); // can't target yourself
  assert.deepStrictEqual(g.hands.map(h => h.length), [1, 4, 2, 4]);
});

test('No Mercy knockout: 25 cards puts one player out and the game goes on without them', () => {
  const used = new Set();
  const [top] = cards('nomercy', num(0, 4), 1, used);
  const [d4] = cards('nomercy', f => f.k === K.DRAW && f.c === 0 && f.n === 4, 1, used);
  const big = cards('nomercy', f => f.k === K.NUM && (f.c === 1 || f.c === 3), 22, used);
  const small = cards('nomercy', f => f.k === K.NUM && f.c === 2, 6, used);
  const g = scenario('nomercy', [[d4, small[0], small[1]], big, small.slice(2, 4), small.slice(4, 6)], top);
  g.play(0, d4, -1, false);
  assert.strictEqual(g.turn, 1);
  assert.strictEqual(g.draw(1), null); // takes the +4: 26 cards
  assert.deepStrictEqual(g.out, [false, true, false, false]);
  assert.strictEqual(g.hands[1].length, 0, 'their cards went back to the draw pile');
  assert.strictEqual(g.over, false);
  assert.strictEqual(g.turn, 2, 'play skips the player who is out');
  assert.deepStrictEqual(g.view(0).out, [0, 1, 0, 0]);
  conservation(g);
  // seat 1 never gets a turn again
  for (let i = 0; i < 6; i++) { assert.notStrictEqual(g.next(i % 4), 1); }
});

test('leaving a 4-player game: the others play on; the last two decide it', () => {
  const g = new Game({ players: 4, seed: 11 });
  g.start(0);
  g.forfeit(0, 'left');
  assert.strictEqual(g.over, false);
  assert.strictEqual(g.out[0], true);
  assert.strictEqual(g.turn, 1, 'the leaver\'s turn passes on');
  conservation(g);
  g.forfeit(2, 'idle');
  assert.strictEqual(g.over, false);
  g.forfeit(3, 'left');
  assert.strictEqual(g.over, true);
  assert.strictEqual(g.winner, 1);
});

test('UNO Flip shows the other side of every opponent\'s cards', () => {
  const g = new Game({ players: 5, seed: 9, mode: 'flip' });
  g.start(0);
  const v = g.view(2);
  assert.strictEqual(v.oppFaces.length, 7 * 4);
  assert.strictEqual(v.counts.length, 5);
  assert.strictEqual(v.out.length, 5);
});

test('bots aim swaps and targeted draws at the player with the fewest cards', () => {
  const g = new Game({ players: 4, seed: 2 });
  g.start(0);
  g.hands[2] = g.hands[2].slice(0, 2);
  assert.strictEqual(fewestCards(g, 0), 2);
  g.dir = -1;
  assert.strictEqual(fewestCards(g, 0), 2);
});

test('bot games at every table size from 3 to 10 in every mode', () => {
  for (const mode of MODE_IDS) {
    for (let n = 3; n <= 10; n++) {
      for (let seed = 1; seed <= 4; seed++) {
        const g = new Game({ players: n, seed: seed * 31 + n, mode });
        g.start();
        let guard = 0;
        while (!g.over && guard++ < 6000) {
          const p = g.turn;
          assert.strictEqual(g.out[p], false, `${mode} ${n}p: a player who is out got a turn`);
          const err = applyMove(g, p, chooseMove(g, p, (seed + n) % 3));
          assert.strictEqual(err, null, `${mode} ${n}p seed ${seed}: ${err}`);
          g.takeEvents();
        }
        assert.ok(g.over, `${mode} ${n}p seed ${seed} never ended`);
        assert.ok(g.winner >= 0 && g.winner < n && !g.out[g.winner], 'winner is still in the game');
        conservation(g);
      }
    }
  }
});
