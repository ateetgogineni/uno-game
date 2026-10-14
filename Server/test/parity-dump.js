'use strict';
// Plays bot-vs-bot games for fixed seeds and prints one line per game. The Unity editor command
// UnoGame.EditorTools.Checks.ParityDump writes the same lines from the C# engine; the two files must match.
//   node test/parity-dump.js > parity-js.txt
const { Game } = require('../src/engine');
const { chooseMove, applyMove } = require('../src/bot');

const lines = [];
for (let seed = 1; seed <= 500; seed++) {
  const g = new Game({ players: 2, seed, stacking: seed % 3 !== 0 });
  g.start();
  const moves = [];
  let guard = 0;
  while (!g.over && guard++ < 5000) {
    const p = g.turn;
    const m = chooseMove(g, p);
    const err = applyMove(g, p, m);
    if (err) { moves.push('ERR'); break; }
    moves.push(m.type === 'play' ? `${p}p${m.card}c${m.color}${m.uno ? 'u' : ''}` : `${p}${m.type[0]}`);
  }
  lines.push(`${seed} w${g.winner} pts${g.points} h${g.hands.map(h => h.length).join('/')} ${moves.join(' ')}`);
}
process.stdout.write(lines.join('\n') + '\n');
