'use strict';
// How often a casual player beats each bot level. The "casual player" plays like the in-game autopilot
// (ViewRules.PickCard in Unity): numbers before actions, wilds last, always calls UNO.
//   node test/difficulty-sim.js [games]
const { Game, cardOf, DRAW2, WILD, WILD4 } = require('../src/engine');
const { chooseMove, applyMove, LEVEL } = require('../src/bot');

function casualMove(g, p) {
  const legal = g.legalMoves(p);
  if (legal.length === 0) return g.hasDrawn ? { type: 'pass' } : { type: 'draw' };
  let best = legal[0], bestScore = -Infinity;
  for (const id of legal) {
    const v = cardOf(id).v;
    let s = v === WILD4 ? 1 : v === WILD ? 2 : v >= 10 ? 30 : 10 + v;
    if (g.pending > 0 && v === DRAW2) s += 100;
    if (s > bestScore) { bestScore = s; best = id; }
  }
  let color = -1;
  if (cardOf(best).c === 4) {
    const counts = [0, 0, 0, 0];
    for (const id of g.hands[p]) if (id !== best && cardOf(id).c < 4) counts[cardOf(id).c]++;
    color = counts.indexOf(Math.max(...counts));
  }
  return { type: 'play', card: best, color, uno: g.hands[p].length === 2 };
}

function winRate(level, games) {
  let wins = 0;
  for (let seed = 1; seed <= games; seed++) {
    const g = new Game({ seed: seed * 7919 + level, stacking: true });
    g.start();
    let guard = 0;
    while (!g.over && guard++ < 5000) {
      const p = g.turn;
      applyMove(g, p, p === 0 ? casualMove(g, 0) : chooseMove(g, 1, level));
    }
    if (g.winner === 0) wins++;
  }
  return wins / games;
}

const games = parseInt(process.argv[2], 10) || 5000;
for (const [name, level] of Object.entries(LEVEL)) {
  console.log(`${name.padEnd(6)} bot: casual player wins ${(winRate(level, games) * 100).toFixed(1)}% of ${games} games`);
}
