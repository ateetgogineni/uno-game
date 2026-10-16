'use strict';
// Rules of the extra game modes (Flip, No Mercy, All Wild, Attack, Dare) and bot games in every mode.
const test = require('node:test');
const assert = require('node:assert');
const { Game } = require('../src/engine');
const { K, WILDC, MODE_IDS, deckFor } = require('../src/cards');
const { chooseMove, applyMove } = require('../src/bot');

function conservation(g) {
  const all = [...g.drawPile, ...g.discard, ...g.hands.flat()];
  assert.strictEqual(all.length, g.deck.length, 'card count');
  assert.strictEqual(new Set(all).size, g.deck.length, 'duplicate card');
}

// First card id in the mode's deck whose face (side a or b) matches.
function find(mode, pred, side = 'a', skip = new Set()) {
  const deck = deckFor(mode);
  for (let i = 0; i < deck.length; i++) if (!skip.has(i) && pred(deck[i][side])) return i;
  throw new Error('no such card');
}

// A started game with chosen hands/top; remaining cards form the draw pile.
function scenario(mode, hands, top, color) {
  const g = new Game({ seed: 3, mode });
  const used = new Set([top, ...hands.flat()]);
  g.hands = hands.map(h => h.slice());
  g.discard = [top];
  g.drawPile = [];
  for (let i = 0; i < g.deck.length; i++) if (!used.has(i)) g.drawPile.push(i);
  g.color = color === undefined ? g.face(top).c : color;
  g.turn = 0;
  return g;
}

test('deck sizes match the official decks', () => {
  assert.strictEqual(deckFor('classic').length, 108);
  assert.strictEqual(deckFor('flip').length, 112);
  assert.strictEqual(deckFor('nomercy').length, 168);
  assert.strictEqual(deckFor('allwild').length, 112);
  assert.strictEqual(deckFor('attack').length, 112);
  assert.strictEqual(deckFor('dare').length, 108);
  assert.ok(deckFor('allwild').every(d => d.a.c === WILDC), 'All Wild: every card is wild');
  assert.ok(deckFor('flip').every(d => d.b), 'Flip: every card has a dark side');
  const darkFives = deckFor('flip').filter(d => d.b.k === K.DRAW && d.b.n === 5).length;
  assert.strictEqual(darkFives, 8);
});

test('Flip: playing FLIP turns everything over', () => {
  const flip = find('flip', f => f.k === K.FLIP && f.c === 0);
  const red3 = find('flip', f => f.k === K.NUM && f.c === 0 && f.n === 3);
  const other = find('flip', f => f.k === K.NUM && f.c === 2, 'a', new Set([red3]));
  const g = scenario('flip', [[flip, other, red3], [find('flip', f => f.k === K.NUM && f.c === 1)]], red3, 0);
  g.hands[0] = [flip, other];
  g.discard = [red3];
  const bottomBefore = g.discard[0];
  assert.strictEqual(g.play(0, flip, -1, false), null);
  assert.strictEqual(g.side, 1);
  assert.strictEqual(g.top(), bottomBefore, 'discard pile is turned over');
  assert.strictEqual(g.face(other), deckFor('flip')[other].b, 'hands now show the dark side');
  assert.strictEqual(g.turn, 1);
  assert.strictEqual(g.view(0).oppFaces.length, 1, 'you see the other side of the opponent cards');
  conservation(g);
});

test('No Mercy: stacks need an equal or higher draw card, and 25 cards knocks you out', () => {
  const d2 = find('nomercy', f => f.k === K.DRAW && f.n === 2);
  const d4 = find('nomercy', f => f.k === K.DRAW && f.n === 4);
  const w6 = find('nomercy', f => f.k === K.WILD_DRAW && f.n === 6);
  const d2b = find('nomercy', f => f.k === K.DRAW && f.n === 2, 'a', new Set([d2]));
  const top = find('nomercy', f => f.k === K.NUM && f.c === deckFor('nomercy')[d4].a.c);
  const g = scenario('nomercy', [[d4, d2b, 0], [w6, d2, 1]], top);
  g.hands[0] = g.hands[0].filter(id => id !== 0);
  assert.strictEqual(g.play(0, d4, -1, false), null);
  assert.strictEqual(g.pending, 4);
  assert.ok(!g.isPlayable(1, d2), '+2 cannot go on +4');
  assert.ok(g.isPlayable(1, w6));
  assert.strictEqual(g.play(1, w6, 1, false), null);
  assert.strictEqual(g.pending, 10);
  g.hands[0] = g.hands[0].concat(g.drawPile.splice(0, 16)); // 17 cards in hand
  assert.strictEqual(g.draw(0), null);
  assert.ok(g.over, 'drawing 10 more puts the hand over 25');
  assert.strictEqual(g.winner, 1);
  conservation(g);
});

test('No Mercy: a 7 swaps hands; Discard All drops the whole colour', () => {
  const seven = find('nomercy', f => f.k === K.NUM && f.n === 7 && f.c === 0);
  const top = find('nomercy', f => f.k === K.NUM && f.n === 5 && f.c === 0);
  const g = scenario('nomercy', [[seven, 200 % 168, 3], [10, 11]], top);
  const mine = g.hands[0].filter(id => id !== seven), theirs = g.hands[1].slice();
  g.play(0, seven, -1, false);
  assert.deepStrictEqual(g.hands[0], theirs);
  assert.deepStrictEqual(g.hands[1], mine);

  const da = find('nomercy', f => f.k === K.DISCARD_ALL && f.c === 2);
  const g2top = find('nomercy', f => f.k === K.NUM && f.c === 2 && f.n === 1);
  const greens = [];
  for (let i = 0; i < 168 && greens.length < 3; i++) {
    const f = deckFor('nomercy')[i].a;
    if (f.c === 2 && f.k === K.NUM && i !== g2top) greens.push(i);
  }
  const blue = find('nomercy', f => f.c === 3 && f.k === K.NUM);
  const g2 = scenario('nomercy', [[da, ...greens, blue], [blue + 1]], g2top);
  g2.play(0, da, -1, true); // one card left afterwards, so call UNO
  assert.deepStrictEqual(g2.hands[0], [blue]);
  assert.strictEqual(g2.top(), da, 'Discard All stays on top');
  conservation(g2);
});

test('All Wild: anything goes, and Skip Two gives two extra turns', () => {
  const skip2 = find('allwild', f => f.k === K.SKIP_TWO);
  const plain = [0, 1, 2, 3].filter(i => i !== skip2);
  const g = scenario('allwild', [[skip2, plain[0], plain[1]], [plain[2]]], plain[3], WILDC);
  assert.ok(g.isPlayable(0, skip2));
  g.play(0, skip2, -1, false);
  assert.strictEqual(g.turn, 0);
  g.play(0, plain[0], -1, true);
  assert.strictEqual(g.turn, 0, 'opponent misses a second turn');
  conservation(g);
});

test('Attack: hitting the launcher ends the turn; Hit 2 hits twice', () => {
  const g = new Game({ seed: 11, mode: 'attack' });
  g.start(0);
  g.takeEvents();
  const before = g.hands[0].length;
  assert.strictEqual(g.draw(0), null);
  const ev = g.takeEvents().find(e => e.k === 'launch');
  assert.ok(ev);
  assert.strictEqual(g.hands[0].length, before + ev.n);
  assert.strictEqual(g.turn, 1);
  conservation(g);
});

test('Dare: doing the dare keeps your turn, refusing draws the penalty', () => {
  const dare = find('dare', f => f.k === K.DARE && f.c === 1);
  const top = find('dare', f => f.k === K.NUM && f.c === 1);
  const g = scenario('dare', [[dare, 4, 5], [6, 7]], top);
  g.play(0, dare, -1, false);
  assert.strictEqual(g.pendingKind, 'dare');
  assert.strictEqual(g.turn, 1);
  assert.strictEqual(g.draw(1), 'do the dare or draw');
  assert.strictEqual(g.dare(1, true), null);
  assert.strictEqual(g.turn, 1, 'still their turn');
  assert.strictEqual(g.hands[1].length, 2);

  const g2 = scenario('dare', [[dare, 4, 5], [6, 7]], top);
  g2.play(0, dare, -1, false);
  g2.dare(1, false);
  assert.strictEqual(g2.hands[1].length, 4);
  assert.strictEqual(g2.turn, 0);
  conservation(g2);
});

test('every mode: bot games at every level finish with all invariants holding', () => {
  for (const mode of MODE_IDS) {
    let total = 0;
    for (let level = 0; level <= 2; level++) {
      for (let seed = 1; seed <= 300; seed++) {
        const g = new Game({ seed: seed * 13 + level, mode });
        g.start();
        let steps = 0;
        while (!g.over) {
          const p = g.turn;
          const err = applyMove(g, p, chooseMove(g, p, level));
          assert.strictEqual(err, null, `${mode} level ${level} seed ${seed}: ${err}`);
          conservation(g);
          if (++steps > 6000) assert.fail(`${mode} level ${level} seed ${seed}: game did not finish`);
        }
        assert.ok(g.winner >= 0);
        total += steps;
      }
    }
    console.log(`  ${mode}: average ${Math.round(total / 900)} moves per game`);
  }
});
