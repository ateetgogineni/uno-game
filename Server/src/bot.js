'use strict';
// Bot player. Mirrored 1:1 in Unity: Assets/UnoGame/Scripts/Core/UnoBot.cs (same rng calls, same order).
const { cardOf, SKIP, REVERSE, DRAW2, WILD, WILD4 } = require('./engine');

// Colour the bot holds most of (ignoring `except`); ties go to the lowest colour index.
function bestColor(game, hand, except) {
  const counts = [0, 0, 0, 0];
  for (const id of hand) {
    if (id === except) continue;
    const c = cardOf(id).c;
    if (c < 4) counts[c]++;
  }
  let best = -1, bestCount = 0;
  for (let c = 0; c < 4; c++) if (counts[c] > bestCount) { best = c; bestCount = counts[c]; }
  return best >= 0 ? best : game.randInt(4);
}

function scoreCard(game, p, id, oppCount) {
  const card = cardOf(id);
  const hand = game.hands[p];
  let sameColor = 0;
  for (const h of hand) if (h !== id && cardOf(h).c === card.c) sameColor++;
  if (card.v === WILD4) return oppCount <= 3 ? 40 : 2;
  if (card.v === WILD) return oppCount <= 2 ? 18 : 4;
  if (card.v === DRAW2) return (oppCount <= 3 ? 34 : 14) + sameColor;
  if (card.v === SKIP || card.v === REVERSE) return (oppCount <= 2 ? 30 : 12) + sameColor;
  return 10 + card.v * 0.5 + sameColor * 1.5;
}

// Returns { type: 'play', card, color, uno } | { type: 'draw' } | { type: 'pass' }.
function chooseMove(game, p) {
  const hand = game.hands[p];
  const legal = game.legalMoves(p);
  if (legal.length === 0) return game.hasDrawn ? { type: 'pass' } : { type: 'draw' };

  let pick = -1;
  if (game.pending > 0) {
    // Pass the stack on, cheapest card first.
    for (const id of legal) if (cardOf(id).v === DRAW2) { pick = id; break; }
    if (pick < 0) pick = legal[0];
  } else {
    const oppCount = game.hands[game.next(p)].length;
    let bestScore = -1e9;
    for (const id of legal) {
      const s = scoreCard(game, p, id, oppCount) + game.rng() * 3;
      if (s > bestScore) { bestScore = s; pick = id; }
    }
  }

  const card = cardOf(pick);
  const color = card.c === 4 ? bestColor(game, hand, pick) : -1;
  const uno = hand.length === 2 ? game.rng() < 0.92 : false;
  return { type: 'play', card: pick, color, uno };
}

function applyMove(game, p, move) {
  if (move.type === 'play') return game.play(p, move.card, move.color, move.uno);
  if (move.type === 'draw') return game.draw(p);
  return game.pass(p);
}

module.exports = { chooseMove, applyMove, bestColor };
