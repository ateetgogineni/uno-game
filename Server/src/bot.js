'use strict';
// Bot player for every mode. Mirrored 1:1 in Unity: Assets/UnoGame/Scripts/Core/UnoBot.cs (same rng calls, same order).
const { K, WILDC } = require('./cards');

// Difficulty. The client asks for one based on the player's record (new players get EASY).
const LEVEL = { EASY: 0, NORMAL: 1, HARD: 2 };

// Per level: chance to pass a +2/+4 stack on (otherwise take the cards), chance to call UNO,
// chance to complete a dare, and (easy only) chance to draw instead of playing / to pass on a playable drawn card.
const STACK_CHANCE = [0.35, 0.7, 1];
const UNO_CHANCE = [0.6, 0.85, 0.92];
const DARE_SUCCESS = [0.5, 0.65, 0.8];
const EASY_DRAW_CHANCE = 0.2;
const EASY_PASS_CHANCE = 0.4;
const NORMAL_RANDOM_CHANCE = 0.25;
// Chance the bot catches a player who forgot to call UNO (official rule: no catch, no penalty).
const CATCH_CHANCE = [0.25, 0.7, 1];

function clampLevel(level) {
  return Number.isInteger(level) && level >= 0 && level <= 2 ? level : LEVEL.HARD;
}

// Colour the bot holds most of (ignoring `except`); ties go to the lowest colour index.
function bestColor(game, hand, except) {
  const counts = [0, 0, 0, 0];
  for (const id of hand) {
    if (id === except) continue;
    const c = game.face(id).c;
    if (c < 4) counts[c]++;
  }
  let best = -1, bestCount = 0;
  for (let c = 0; c < 4; c++) if (counts[c] > bestCount) { best = c; bestCount = counts[c]; }
  return best >= 0 ? best : game.randInt(4);
}

function isSwap(game, f) {
  return f.k === K.SWAP || (game.cfg.sevenZero && f.k === K.NUM && (f.n === 7 || f.n === 0));
}

function scoreCard(game, p, id, oppCount) {
  const f = game.face(id);
  const hand = game.hands[p];
  let sameColor = 0;
  for (const h of hand) if (h !== id && game.face(h).c === f.c) sameColor++;
  if (isSwap(game, f)) return hand.length - 1 > oppCount ? 45 : -20;
  if (game.cfg.allWild) {
    if (f.k === K.WILD_DRAW) return oppCount <= 3 ? 40 : 16;
    if (f.k === K.TARGET_DRAW) return 18;
    if (f.k === K.WILD) return 10;
    return 14;
  }
  switch (f.k) {
    case K.WILD_DRAW: case K.WILD_REV_DRAW: case K.WILD_DRAW_COLOR: case K.ROULETTE:
    case K.HIT_FIRE: case K.HIT_ALL: case K.WILD_DARE:
      return oppCount <= 3 ? 40 : 2;
    case K.WILD:
      return oppCount <= 2 ? 18 : 4;
    case K.DRAW: case K.HIT: case K.DARE:
      return (oppCount <= 3 ? 34 : 14) + sameColor;
    case K.SKIP: case K.REVERSE: case K.SKIP_ALL: case K.SKIP_TWO:
      return (oppCount <= 2 ? 30 : 12) + sameColor;
    case K.DISCARD_ALL:
      return 10 + sameColor * 6;
    case K.FLIP:
      return 12 + sameColor;
    case K.TARGET_DRAW:
      return 20;
    default:
      return 10 + f.n * 0.5 + sameColor * 1.5;
  }
}

// Returns { type: 'play', card, color, uno } | { type: 'draw' } | { type: 'pass' }
//       | { type: 'roulette', color } | { type: 'dare', ok }.
//   HARD   plays the best card, saves wilds, punishes a nearly-empty hand, always stacks.
//   NORMAL plays a random card a quarter of the time and sometimes takes a stack.
//   EASY   prefers plain number cards, picks wild colours at random, often takes stacks,
//          sometimes draws instead of playing and forgets UNO more often.
function chooseMove(game, p, level = LEVEL.HARD) {
  level = clampLevel(level);
  if (game.pendingKind === 'roulette') return { type: 'roulette', color: game.randInt(4) };
  if (game.pendingKind === 'dare') return { type: 'dare', ok: game.rng() < DARE_SUCCESS[level] };

  const hand = game.hands[p];
  const legal = game.legalMoves(p);
  if (legal.length === 0) return game.hasDrawn ? { type: 'pass' } : { type: 'draw' };

  if (level === LEVEL.EASY) {
    if (game.hasDrawn) {
      if (game.rng() < EASY_PASS_CHANCE) return { type: 'pass' };
    } else if (game.pendingKind === '' && game.rng() < EASY_DRAW_CHANCE) {
      return { type: 'draw' };
    }
  }

  let pick = -1;
  if (game.pendingKind === 'stack') {
    if (STACK_CHANCE[level] < 1 && game.rng() >= STACK_CHANCE[level]) return { type: 'draw' };
    // Pass the stack on with the smallest card that still counts.
    for (const id of legal) if (pick < 0 || game.face(id).n < game.face(pick).n) pick = id;
  } else if (level === LEVEL.EASY) {
    const plain = legal.filter(id => game.face(id).k === K.NUM);
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

  const f = game.face(pick);
  let color = -1;
  if (f.c === WILDC && !game.cfg.allWild) color = level === LEVEL.EASY && game.rng() < 0.5 ? game.randInt(4) : bestColor(game, hand, pick);
  const uno = hand.length === 2 ? game.rng() < UNO_CHANCE[level] : false;
  return { type: 'play', card: pick, color, uno };
}

function applyMove(game, p, move) {
  switch (move.type) {
    case 'play': return game.play(p, move.card, move.color, move.uno);
    case 'draw': return game.draw(p);
    case 'roulette': return game.roulette(p, move.color);
    case 'dare': return game.dare(p, move.ok);
    default: return game.pass(p);
  }
}

module.exports = { chooseMove, applyMove, bestColor, clampLevel, LEVEL, CATCH_CHANCE };
