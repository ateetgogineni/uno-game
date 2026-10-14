'use strict';
// Bot player. Mirrored 1:1 in Unity: Assets/UnoGame/Scripts/Core/UnoBot.cs (same rng calls, same order).
const { cardOf, SKIP, REVERSE, DRAW2, WILD, WILD4 } = require('./engine');

// Difficulty. The client asks for one based on the player's record (new players get EASY).
const LEVEL = { EASY: 0, NORMAL: 1, HARD: 2 };

// Per level: chance to pass a +2/+4 stack on (otherwise take the cards), chance to call UNO,
// and (easy only) chance to draw instead of playing / to pass on a playable drawn card.
const STACK_CHANCE = [0.35, 0.7, 1];
const UNO_CHANCE = [0.6, 0.85, 0.92];
// Chance the bot catches a player who forgot to call UNO (official rule: no catch, no penalty).
const CATCH_CHANCE = [0.25, 0.7, 1];
const EASY_DRAW_CHANCE = 0.2;
const EASY_PASS_CHANCE = 0.4;
const NORMAL_RANDOM_CHANCE = 0.25;

function clampLevel(level) {
  return Number.isInteger(level) && level >= 0 && level <= 2 ? level : LEVEL.HARD;
}

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
//   HARD   plays the best card, saves wilds, punishes a nearly-empty hand, always stacks.
//   NORMAL plays a random card a quarter of the time and sometimes takes a stack.
//   EASY   prefers plain number cards, picks wild colours at random, often takes stacks,
//          sometimes draws instead of playing and forgets UNO more often.
function chooseMove(game, p, level = LEVEL.HARD) {
  level = clampLevel(level);
  const hand = game.hands[p];
  const legal = game.legalMoves(p);
  if (legal.length === 0) return game.hasDrawn ? { type: 'pass' } : { type: 'draw' };

  if (level === LEVEL.EASY) {
    if (game.hasDrawn) {
      if (game.rng() < EASY_PASS_CHANCE) return { type: 'pass' };
    } else if (game.pending === 0 && game.rng() < EASY_DRAW_CHANCE) {
      return { type: 'draw' };
    }
  }

  let pick = -1;
  if (game.pending > 0) {
    if (STACK_CHANCE[level] < 1 && game.rng() >= STACK_CHANCE[level]) return { type: 'draw' };
    // Pass the stack on, cheapest card first.
    for (const id of legal) if (cardOf(id).v === DRAW2) { pick = id; break; }
    if (pick < 0) pick = legal[0];
  } else if (level === LEVEL.EASY) {
    const plain = legal.filter(id => cardOf(id).v <= 9);
    const pool = plain.length > 0 && game.rng() < 0.75 ? plain : legal;
    pick = pool[game.randInt(pool.length)];
  } else if (level === LEVEL.NORMAL && game.rng() < NORMAL_RANDOM_CHANCE) {
    pick = legal[game.randInt(legal.length)];
  } else {
    const oppCount = game.hands[game.next(p)].length;
    let bestScore = -1e9;
    for (const id of legal) {
      const s = scoreCard(game, p, id, oppCount) + game.rng() * 3;
      if (s > bestScore) { bestScore = s; pick = id; }
    }
  }

  const card = cardOf(pick);
  let color = -1;
  if (card.c === 4) color = level === LEVEL.EASY && game.rng() < 0.5 ? game.randInt(4) : bestColor(game, hand, pick);
  const uno = hand.length === 2 ? game.rng() < UNO_CHANCE[level] : false;
  return { type: 'play', card: pick, color, uno };
}

function applyMove(game, p, move) {
  if (move.type === 'play') return game.play(p, move.card, move.color, move.uno);
  if (move.type === 'draw') return game.draw(p);
  return game.pass(p);
}

module.exports = { chooseMove, applyMove, bestColor, clampLevel, LEVEL, CATCH_CHANCE };
