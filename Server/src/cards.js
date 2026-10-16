'use strict';
// Card faces and decks for every game mode. Mirrored 1:1 in Unity: Assets/UnoGame/Scripts/Core/Decks.cs.
//
// A card id is an index into the mode's deck. Each card has face `a` (and, in UNO Flip, a dark face
// `b`). A face is { c, k, n }:
//   c  colour 0..3 (light: red yellow green blue; Flip dark side: pink teal orange purple), 4 = wild
//   k  kind (below)
//   n  number for NUM, card count for draw/hit/dare cards

const K = {
  NUM: 0, SKIP: 1, REVERSE: 2, DRAW: 3, WILD: 4, WILD_DRAW: 5, FLIP: 6, SKIP_ALL: 7, WILD_DRAW_COLOR: 8,
  DISCARD_ALL: 9, WILD_REV_DRAW: 10, ROULETTE: 11, SKIP_TWO: 12, TARGET_DRAW: 13, SWAP: 14,
  HIT: 15, HIT_ALL: 16, HIT_FIRE: 17, DARE: 18, WILD_DARE: 19,
};
const WILDC = 4;

const MODE_IDS = ['classic', 'flip', 'nomercy', 'allwild', 'attack', 'dare'];

function face(c, k, n = 0) { return Object.freeze({ c, k, n }); }
function faceCode(f) { return f.c * 1024 + f.k * 32 + f.n; }

// mulberry32 again (cards.js must not depend on engine.js); used only to pair Flip's two sides.
function pairRng(seed) {
  let a = seed | 0;
  return function () {
    a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function classicDeck() {
  const d = [];
  for (let c = 0; c < 4; c++) {
    d.push(face(c, K.NUM, 0));
    for (let v = 1; v <= 12; v++) {
      const f = v <= 9 ? face(c, K.NUM, v) : v === 10 ? face(c, K.SKIP) : v === 11 ? face(c, K.REVERSE) : face(c, K.DRAW, 2);
      d.push(f, f);
    }
  }
  for (let i = 0; i < 4; i++) d.push(face(WILDC, K.WILD));
  for (let i = 0; i < 4; i++) d.push(face(WILDC, K.WILD_DRAW, 4));
  return d.map(a => ({ a, b: null }));
}

function flipDeck() {
  const light = [], dark = [];
  for (let c = 0; c < 4; c++) {
    for (let v = 1; v <= 9; v++) light.push(face(c, K.NUM, v), face(c, K.NUM, v));
    for (const f of [face(c, K.DRAW, 1), face(c, K.REVERSE), face(c, K.SKIP), face(c, K.FLIP)]) light.push(f, f);
  }
  for (let i = 0; i < 4; i++) light.push(face(WILDC, K.WILD));
  for (let i = 0; i < 4; i++) light.push(face(WILDC, K.WILD_DRAW, 2));
  for (let c = 0; c < 4; c++) {
    for (let v = 1; v <= 9; v++) dark.push(face(c, K.NUM, v), face(c, K.NUM, v));
    for (const f of [face(c, K.DRAW, 5), face(c, K.REVERSE), face(c, K.SKIP_ALL), face(c, K.FLIP)]) dark.push(f, f);
  }
  for (let i = 0; i < 4; i++) dark.push(face(WILDC, K.WILD));
  for (let i = 0; i < 4; i++) dark.push(face(WILDC, K.WILD_DRAW_COLOR));
  // Like a printed deck, each card has a fixed (but unpredictable) dark side.
  const rng = pairRng(20190706);
  for (let i = dark.length - 1; i > 0; i--) {
    const j = Math.floor(rng() * (i + 1));
    const t = dark[i]; dark[i] = dark[j]; dark[j] = t;
  }
  return light.map((a, i) => ({ a, b: dark[i] }));
}

function noMercyDeck() {
  const d = [];
  for (let c = 0; c < 4; c++) {
    for (let v = 0; v <= 9; v++) d.push(face(c, K.NUM, v), face(c, K.NUM, v));
    for (let i = 0; i < 3; i++) d.push(face(c, K.SKIP), face(c, K.REVERSE), face(c, K.DRAW, 2), face(c, K.DISCARD_ALL));
    for (let i = 0; i < 2; i++) d.push(face(c, K.DRAW, 4), face(c, K.SKIP_ALL));
  }
  for (let i = 0; i < 8; i++) d.push(face(WILDC, K.WILD_REV_DRAW, 4));
  for (let i = 0; i < 4; i++) d.push(face(WILDC, K.WILD_DRAW, 6));
  for (let i = 0; i < 4; i++) d.push(face(WILDC, K.WILD_DRAW, 10));
  for (let i = 0; i < 8; i++) d.push(face(WILDC, K.ROULETTE));
  return d.map(a => ({ a, b: null }));
}

function allWildDeck() {
  const d = [];
  const add = (f, count) => { for (let i = 0; i < count; i++) d.push(f); };
  add(face(WILDC, K.WILD), 54);
  add(face(WILDC, K.REVERSE), 14);
  add(face(WILDC, K.SKIP), 14);
  add(face(WILDC, K.SKIP_TWO), 6);
  add(face(WILDC, K.WILD_DRAW, 2), 10);
  add(face(WILDC, K.WILD_DRAW, 4), 6);
  add(face(WILDC, K.TARGET_DRAW, 2), 4);
  add(face(WILDC, K.SWAP), 4);
  return d.map(a => ({ a, b: null }));
}

function attackDeck() {
  const d = [];
  for (let c = 0; c < 4; c++) {
    for (let v = 1; v <= 9; v++) d.push(face(c, K.NUM, v), face(c, K.NUM, v));
    for (const f of [face(c, K.HIT, 2), face(c, K.REVERSE), face(c, K.SKIP)]) d.push(f, f);
    d.push(face(c, K.DISCARD_ALL), face(c, K.SWAP));
  }
  for (let i = 0; i < 4; i++) d.push(face(WILDC, K.WILD));
  for (let i = 0; i < 2; i++) d.push(face(WILDC, K.HIT_FIRE));
  for (let i = 0; i < 2; i++) d.push(face(WILDC, K.HIT_ALL));
  return d.map(a => ({ a, b: null }));
}

function dareDeck() {
  const d = [];
  for (let c = 0; c < 4; c++) {
    d.push(face(c, K.NUM, 0));
    for (let v = 1; v <= 9; v++) d.push(face(c, K.NUM, v), face(c, K.NUM, v));
    for (const f of [face(c, K.REVERSE), face(c, K.SKIP), face(c, K.DARE, 2)]) d.push(f, f);
  }
  for (let i = 0; i < 8; i++) d.push(face(WILDC, K.WILD_DARE, 4));
  return d.map(a => ({ a, b: null }));
}

// Rules per mode.
//   stack            'classic' (+2/+4 on any +2/+4) | 'nomercy' (any draw card of equal or higher value) | null
//   restrictWildDraw Wild draw cards only without a card of the current colour (official classic/Flip rule)
//   drawUntil        with nothing to play, keep drawing until a playable card comes up (No Mercy)
//   mercy            hand size at which a player is out (No Mercy)
//   sevenZero        7 and 0 swap hands (No Mercy; with two players both are a swap)
//   allWild          every card plays on every card; no colours
//   launcher         drawing is replaced by hitting the card launcher (Attack)
//   dares            Dare cards: do a dare or draw (Dare)
const MODES = {
  classic: { deck: classicDeck, stack: 'classic', restrictWildDraw: true },
  flip: { deck: flipDeck, stack: null, restrictWildDraw: true, flip: true },
  nomercy: { deck: noMercyDeck, stack: 'nomercy', drawUntil: true, mercy: 25, sevenZero: true },
  allwild: { deck: allWildDeck, stack: null, allWild: true },
  attack: { deck: attackDeck, stack: null, launcher: true },
  dare: { deck: dareDeck, stack: null, dares: true },
};

const deckCache = {};
function deckFor(mode) {
  if (!deckCache[mode]) deckCache[mode] = Object.freeze(MODES[mode].deck());
  return deckCache[mode];
}

function isDrawCard(f) { return f.k === K.DRAW || f.k === K.WILD_DRAW || f.k === K.WILD_REV_DRAW; }

// Points the winner scores for a card left in a loser's hand.
function points(mode, f, side) {
  if (mode === 'flip') {
    if (f.k === K.NUM) return f.n;
    if (side === 0) return f.k === K.DRAW ? 10 : f.k === K.WILD ? 40 : f.k === K.WILD_DRAW ? 50 : 20;
    return f.k === K.SKIP_ALL ? 30 : f.k === K.WILD ? 40 : f.k === K.WILD_DRAW_COLOR ? 60 : 20;
  }
  if (mode === 'allwild') return f.k === K.WILD ? 20 : 50;
  if (f.k === K.NUM) return f.n;
  return f.c === WILDC ? 50 : 20;
}

module.exports = { K, WILDC, MODE_IDS, MODES, deckFor, face, faceCode, isDrawCard, points };
