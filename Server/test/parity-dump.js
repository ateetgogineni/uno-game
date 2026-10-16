'use strict';
// Plays bot-vs-bot games for fixed seeds and prints one line per game. The Unity editor command
// UnoGame.EditorTools.Checks.ParityDump writes the same lines from the C# engine; the two files must match.
//   node test/parity-dump.js > test/parity-js.txt
const { Game } = require('../src/engine');
const { MODE_IDS } = require('../src/cards');
const { chooseMove, applyMove } = require('../src/bot');

const lines = [];
// 600 two-player games, then 240 games of 3 to 10 players.
for (let seed = 1; seed <= 840; seed++) {
  const mode = MODE_IDS[seed % MODE_IDS.length];          // every mode
  const level = Math.floor(seed / 7) % 3;                 // easy, normal and hard bots
  const players = seed <= 600 ? 2 : 3 + (seed % 8);
  const g = new Game({ players, seed, stacking: seed % 3 !== 0, mode });
  g.start();
  const moves = [];
  let guard = 0;
  while (!g.over && guard++ < 5000) {
    const p = g.turn;
    const m = chooseMove(g, p, level);
    const err = applyMove(g, p, m);
    if (err) { moves.push('ERR'); break; }
    if (m.type === 'play') moves.push(`${p}p${m.card}c${m.color}${m.uno ? 'u' : ''}${m.target >= 0 ? 't' + m.target : ''}`);
    else if (m.type === 'roulette') moves.push(`${p}r${m.color}`);
    else if (m.type === 'dare') moves.push(`${p}x${m.ok ? 1 : 0}`);
    else moves.push(`${p}${m.type[0]}`);
  }
  const outs = players > 2 ? ` o${g.out.map(o => (o ? 1 : 0)).join('')}` : '';
  lines.push(`${seed} ${mode} w${g.winner} pts${g.points} s${g.side} h${g.hands.map(h => h.length).join('/')}${outs} ${moves.join(' ')}`);
}
process.stdout.write(lines.join('\n') + '\n');
