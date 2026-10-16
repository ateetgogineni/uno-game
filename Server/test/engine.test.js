'use strict';
const test = require('node:test');
const assert = require('node:assert');
const { Game, cardOf, cardPoints, SKIP, REVERSE, DRAW2, WILD, WILD4 } = require('../src/engine');
const { chooseMove, applyMove } = require('../src/bot');

// Card id helpers for hand-built scenarios.
const id = (c, v, copy = 0) => {
  for (let i = 0; i < 108; i++) {
    const k = cardOf(i);
    if (k.c === c && k.v === v) { if (copy === 0) return i; copy--; }
  }
  throw new Error('no card');
};
const RED = 0, YELLOW = 1, GREEN = 2, BLUE = 3, W = 4;

// A started game with chosen hands/top. Remaining cards go to the draw pile.
function scenario({ hands, top, color, turn = 0, stacking = true }) {
  const g = new Game({ players: hands.length, seed: 7, stacking });
  const used = new Set([top, ...hands.flat()]);
  g.hands = hands.map(h => h.slice());
  g.discard = [top];
  g.drawPile = [];
  for (let i = 0; i < 108; i++) if (!used.has(i)) g.drawPile.push(i);
  g.color = color === undefined ? cardOf(top).c : color;
  g.turn = turn;
  return g;
}

function checkConservation(g) {
  const all = [...g.drawPile, ...g.discard, ...g.hands.flat()];
  assert.strictEqual(all.length, g.deck.length, 'card count');
  assert.strictEqual(new Set(all).size, g.deck.length, 'duplicate card');
}

test('deck has the official 108 cards', () => {
  const counts = {};
  for (let i = 0; i < 108; i++) {
    const k = cardOf(i);
    counts[k.c + ':' + k.v] = (counts[k.c + ':' + k.v] || 0) + 1;
  }
  for (let c = 0; c < 4; c++) {
    assert.strictEqual(counts[c + ':0'], 1);
    for (let v = 1; v <= 12; v++) assert.strictEqual(counts[c + ':' + v], 2);
  }
  assert.strictEqual(counts['4:13'], 4);
  assert.strictEqual(counts['4:14'], 4);
  assert.strictEqual(cardPoints(id(RED, 7)), 7);
  assert.strictEqual(cardPoints(id(BLUE, SKIP)), 20);
  assert.strictEqual(cardPoints(id(W, WILD4)), 50);
});

test('start deals 7 each and flips a number card', () => {
  for (let seed = 1; seed < 300; seed++) {
    const g = new Game({ seed });
    g.start();
    assert.strictEqual(g.hands[0].length, 7);
    assert.strictEqual(g.hands[1].length, 7);
    assert.ok(cardOf(g.top()).v <= 9);
    assert.strictEqual(g.color, cardOf(g.top()).c);
    checkConservation(g);
  }
});

test('colour, number and symbol matching', () => {
  const g = scenario({ hands: [[id(RED, 5), id(BLUE, 7), id(GREEN, 3), id(YELLOW, SKIP)], [id(RED, 1)]], top: id(BLUE, 5) });
  assert.ok(g.isPlayable(0, id(RED, 5)), 'same number');
  assert.ok(g.isPlayable(0, id(BLUE, 7)), 'same colour');
  assert.ok(!g.isPlayable(0, id(GREEN, 3)), 'no match');
  assert.ok(!g.isPlayable(0, id(YELLOW, SKIP)), 'no match');
  assert.strictEqual(g.play(0, id(GREEN, 3), -1, false), 'card cannot be played');
});

test('wild draw four only without a card of the current colour', () => {
  const g = scenario({ hands: [[id(W, WILD4), id(BLUE, 2), id(RED, 9)], [id(RED, 1)]], top: id(BLUE, 5) });
  assert.ok(!g.isPlayable(0, id(W, WILD4)));
  const g2 = scenario({ hands: [[id(W, WILD4), id(GREEN, 5), id(RED, 9)], [id(RED, 1)]], top: id(BLUE, 5) });
  assert.ok(g2.isPlayable(0, id(W, WILD4)), 'matching number does not block it');
  assert.strictEqual(g2.play(0, id(W, WILD4), -1, false), 'choose a colour');
  assert.strictEqual(g2.play(0, id(W, WILD4), GREEN, false), null);
  assert.strictEqual(g2.color, GREEN);
  assert.strictEqual(g2.pending, 4);
  assert.strictEqual(g2.turn, 1);
});

test('two players: skip and reverse give the same player another turn', () => {
  const g = scenario({ hands: [[id(RED, SKIP), id(RED, REVERSE), id(RED, 3)], [id(BLUE, 1), id(BLUE, 2)]], top: id(RED, 5) });
  assert.strictEqual(g.play(0, id(RED, SKIP), -1, false), null);
  assert.strictEqual(g.turn, 0);
  assert.strictEqual(g.play(0, id(RED, REVERSE), -1, false), null);
  assert.strictEqual(g.turn, 0);
  assert.strictEqual(g.dir, -1);
});

test('stacking: +2 on +2 then the next player takes all', () => {
  const g = scenario({ hands: [[id(RED, DRAW2), id(RED, 1), id(RED, 2)], [id(BLUE, DRAW2), id(BLUE, 3), id(BLUE, 4)]], top: id(RED, 5) });
  g.play(0, id(RED, DRAW2), -1, false);
  assert.strictEqual(g.pending, 2);
  assert.ok(!g.isPlayable(1, id(BLUE, 3)), 'only draw cards while a stack is pending');
  assert.strictEqual(g.play(1, id(BLUE, DRAW2), -1, false), null, 'any colour +2 stacks');
  assert.strictEqual(g.pending, 4);
  assert.strictEqual(g.turn, 0);
  assert.strictEqual(g.draw(0), null);
  assert.strictEqual(g.hands[0].length, 6);
  assert.strictEqual(g.pending, 0);
  assert.strictEqual(g.turn, 1);
  checkConservation(g);
});

test('without stacking +2 makes the opponent draw and skips them', () => {
  const g = scenario({ hands: [[id(RED, DRAW2), id(RED, 1), id(RED, 2)], [id(BLUE, 3)]], top: id(RED, 5), stacking: false });
  g.play(0, id(RED, DRAW2), -1, false);
  assert.strictEqual(g.hands[1].length, 3);
  assert.strictEqual(g.turn, 0);
});

test('forgetting UNO costs two cards; calling it does not', () => {
  const g = scenario({ hands: [[id(RED, 1), id(RED, 2)], [id(BLUE, 3), id(BLUE, 4)]], top: id(RED, 5) });
  g.play(0, id(RED, 1), -1, false);
  assert.strictEqual(g.hands[0].length, 3);
  const g2 = scenario({ hands: [[id(RED, 1), id(RED, 2)], [id(BLUE, 3), id(BLUE, 4)]], top: id(RED, 5) });
  g2.play(0, id(RED, 1), -1, true);
  assert.strictEqual(g2.hands[0].length, 1);
  assert.ok(g2.takeEvents().some(e => e.k === 'uno'));
});

test('drawing a playable card: play it or pass, nothing else', () => {
  const g = scenario({ hands: [[id(GREEN, 1)], [id(BLUE, 3)]], top: id(RED, 5) });
  g.drawPile.push(id(RED, 8, 1)); // next draw
  assert.strictEqual(g.draw(0), null);
  assert.strictEqual(g.turn, 0);
  assert.strictEqual(g.hasDrawn, true);
  assert.strictEqual(g.play(0, id(GREEN, 1), -1, false), 'only the drawn card can be played');
  assert.strictEqual(g.draw(0), 'already drew this turn');
  assert.strictEqual(g.pass(0), null);
  assert.strictEqual(g.turn, 1);
});

test('drawing an unplayable card ends the turn', () => {
  const g = scenario({ hands: [[id(GREEN, 1)], [id(BLUE, 3)]], top: id(RED, 5) });
  g.drawPile.push(id(BLUE, 9));
  g.draw(0);
  assert.strictEqual(g.turn, 1);
});

test('last card +2 still makes the next player draw, and those cards score', () => {
  const g = scenario({ hands: [[id(RED, DRAW2)], [id(BLUE, 3)]], top: id(RED, 5) });
  g.drawPile.push(id(BLUE, 9), id(BLUE, 8));
  g.play(0, id(RED, DRAW2), -1, false);
  assert.ok(g.over);
  assert.strictEqual(g.winner, 0);
  assert.strictEqual(g.hands[1].length, 3);
  assert.strictEqual(g.points, 3 + 9 + 8);
});

test('empty draw pile reshuffles the discard pile', () => {
  const g = scenario({ hands: [[id(GREEN, 1)], [id(BLUE, 3)]], top: id(RED, 5) });
  g.discard = [...g.drawPile, g.discard[0]];
  g.drawPile = [];
  g.draw(0);
  assert.ok(g.takeEvents().some(e => e.k === 'reshuffle'));
  checkConservation(g);
});

test('timeouts: autoMove always ends the turn', () => {
  for (let seed = 1; seed < 200; seed++) {
    const g = new Game({ seed });
    g.start(0);
    g.autoMove(0);
    assert.ok(g.turn === 1 || g.over);
  }
});

test('a forgotten UNO that nobody catches costs nothing', () => {
  const g = scenario({ hands: [[id(RED, 1), id(RED, 2)], [id(BLUE, 3), id(BLUE, 4)]], top: id(RED, 5) });
  g.play(0, id(RED, 1), -1, false, true);
  assert.strictEqual(g.hands[0].length, 1);
  assert.ok(!g.takeEvents().some(e => e.k === 'unoPen'));
});

test('every bot level only makes legal moves', () => {
  for (let level = 0; level <= 2; level++) {
    for (let seed = 1; seed <= 600; seed++) {
      const g = new Game({ seed: seed * 31 + level, stacking: true });
      g.start();
      let steps = 0;
      while (!g.over) {
        const p = g.turn;
        const err = applyMove(g, p, chooseMove(g, p, level));
        assert.strictEqual(err, null, `level ${level} seed ${seed}: ${err}`);
        checkConservation(g);
        if (++steps > 5000) assert.fail(`level ${level} seed ${seed}: game did not finish`);
      }
    }
  }
});

test('3000 bot games finish with all invariants holding', () => {
  let totalTurns = 0, longest = 0;
  for (let seed = 1; seed <= 3000; seed++) {
    const g = new Game({ seed, stacking: seed % 2 === 0 });
    g.start();
    let steps = 0;
    while (!g.over) {
      const p = g.turn;
      const before = g.hands[p].length;
      const move = chooseMove(g, p);
      const err = applyMove(g, p, move);
      assert.strictEqual(err, null, `seed ${seed}: bot made an illegal move ${JSON.stringify(move)}: ${err}`);
      if (move.type === 'play') assert.ok(g.hands[p].length < before || g.over || g.hands[p].length === before + 1, 'play removes a card (or UNO penalty)');
      checkConservation(g);
      if (++steps > 5000) assert.fail(`seed ${seed}: game did not finish`);
    }
    assert.ok(g.winner >= 0);
    assert.strictEqual(g.hands[g.winner].length, 0);
    totalTurns += steps;
    longest = Math.max(longest, steps);
  }
  console.log(`  average ${Math.round(totalTurns / 3000)} moves per game, longest ${longest}`);
});
