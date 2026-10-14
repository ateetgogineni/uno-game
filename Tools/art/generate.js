'use strict';
// Production art generator. Draws every sprite as SVG, lays sprites out in atlases, renders each atlas
// to PNG with headless Microsoft Edge, and writes a JSON sprite map that the Unity importer
// (Assets/UnoGame/Editor/ArtImporter.cs) uses to slice the textures.
//   node Tools/art/generate.js
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const ROOT = path.resolve(__dirname, '..', '..');
const OUT = path.join(ROOT, 'Assets', 'UnoGame', 'Art', 'Generated');
const WORK = path.join(__dirname, 'out');
const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';

const COL = {
  red: { base: '#E8333C', light: '#FF7466', dark: '#9C1521' },
  yellow: { base: '#F8BF1F', light: '#FFE27A', dark: '#B67C00' },
  green: { base: '#39AE4A', light: '#8BDD6E', dark: '#18672A' },
  blue: { base: '#1E85E0', light: '#72BEFF', dark: '#0B4A9A' },
  wild: { base: '#2A2040', light: '#4B3B72', dark: '#130D21' },
};
const ORDER = ['red', 'yellow', 'green', 'blue'];
const INK = '#1B1830';
const FONT = "font-family:'Segoe UI',sans-serif;font-weight:900;font-style:italic";
const UIFONT = "font-family:'Segoe UI',sans-serif;font-weight:800";

let uid = 0;
const id = p => `${p}${++uid}`;
const f = n => +n.toFixed(2);

function svg(w, h, body, defs = '') {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}"><defs>${defs}</defs>${body}</svg>`;
}

function rrect(x, y, w, h, r, attrs) {
  return `<rect x="${f(x)}" y="${f(y)}" width="${f(w)}" height="${f(h)}" rx="${f(r)}" ${attrs}/>`;
}

// Rounded rectangle with separate corner radii (tl, tr, br, bl) — the card "leaf" emblem.
function leafPath(x0, y0, w, h, tl, tr, br, bl) {
  const x1 = x0 + w, y1 = y0 + h;
  return `M${f(x0 + tl)} ${f(y0)} L${f(x1 - tr)} ${f(y0)} A${tr} ${tr} 0 0 1 ${f(x1)} ${f(y0 + tr)} ` +
    `L${f(x1)} ${f(y1 - br)} A${br} ${br} 0 0 1 ${f(x1 - br)} ${f(y1)} L${f(x0 + bl)} ${f(y1)} ` +
    `A${bl} ${bl} 0 0 1 ${f(x0)} ${f(y1 - bl)} L${f(x0)} ${f(y0 + tl)} A${tl} ${tl} 0 0 1 ${f(x0 + tl)} ${f(y0)} Z`;
}

function text(x, y, size, str, fill, stroke, sw, extra = '', style = FONT) {
  return `<text x="${f(x)}" y="${f(y)}" font-size="${size}" text-anchor="middle" dominant-baseline="central" ` +
    `style="${style}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}" stroke-linejoin="round" paint-order="stroke" ${extra}>${str}</text>`;
}

// ---------- card symbols ----------

function skipShapes(cx, cy, r, sw) {
  const d = r * Math.SQRT1_2;
  return [
    `<circle cx="${f(cx)}" cy="${f(cy)}" r="${f(r)}"/>`,
    `<line x1="${f(cx - d)}" y1="${f(cy + d)}" x2="${f(cx + d)}" y2="${f(cy - d)}"/>`,
  ];
}

function skipIcon(cx, cy, r, sw, fill, outline, ow) {
  const s = skipShapes(cx, cy, r, sw).join('');
  return `<g fill="none" stroke-linecap="round"><g stroke="${outline}" stroke-width="${sw + ow}">${s}</g>` +
    `<g stroke="${fill}" stroke-width="${sw}">${s}</g></g>`;
}

function reverseArrow(cx, cy, r, a0, a1, head) {
  const rad = a => a * Math.PI / 180;
  const p0 = [cx + r * Math.cos(rad(a0)), cy + r * Math.sin(rad(a0))];
  const p1 = [cx + r * Math.cos(rad(a1)), cy + r * Math.sin(rad(a1))];
  const t = [-Math.sin(rad(a1)), Math.cos(rad(a1))];
  const n = [Math.cos(rad(a1)), Math.sin(rad(a1))];
  const tip = [p1[0] + t[0] * head * 1.15, p1[1] + t[1] * head * 1.15];
  const b1 = [p1[0] + n[0] * head - t[0] * head * 0.2, p1[1] + n[1] * head - t[1] * head * 0.2];
  const b2 = [p1[0] - n[0] * head - t[0] * head * 0.2, p1[1] - n[1] * head - t[1] * head * 0.2];
  return {
    arc: `<path d="M${f(p0[0])} ${f(p0[1])} A${r} ${r} 0 0 1 ${f(p1[0])} ${f(p1[1])}"/>`,
    head: `<polygon points="${f(tip[0])},${f(tip[1])} ${f(b1[0])},${f(b1[1])} ${f(b2[0])},${f(b2[1])}"/>`,
  };
}

function reverseIcon(cx, cy, r, sw, fill, outline, ow) {
  const head = sw * 1.25;
  const a = reverseArrow(cx, cy, r, 195, 318, head);
  const b = reverseArrow(cx, cy, r, 15, 138, head);
  const arcs = a.arc + b.arc, heads = a.head + b.head;
  return `<g stroke-linecap="round" stroke-linejoin="round">` +
    `<g fill="none" stroke="${outline}" stroke-width="${sw + ow}">${arcs}</g>` +
    `<g fill="${outline}" stroke="${outline}" stroke-width="${ow}">${heads}</g>` +
    `<g fill="none" stroke="${fill}" stroke-width="${sw}">${arcs}</g>` +
    `<g fill="${fill}">${heads}</g></g>`;
}

function miniCard(cx, cy, w, h, rot, fill, outline, ow) {
  return `<g transform="rotate(${rot} ${f(cx)} ${f(cy)})">` +
    rrect(cx - w / 2, cy - h / 2, w, h, w * 0.16, `fill="#fff" stroke="${outline}" stroke-width="${ow}"`) +
    rrect(cx - w / 2 + w * 0.12, cy - h / 2 + w * 0.12, w * 0.76, h - w * 0.24, w * 0.1, `fill="${fill}"`) + `</g>`;
}

function pie(cx, cy, r, outline, ow) {
  const q = (a0, col) => {
    const rad = a => a * Math.PI / 180;
    const x0 = cx + r * Math.cos(rad(a0)), y0 = cy + r * Math.sin(rad(a0));
    const x1 = cx + r * Math.cos(rad(a0 + 90)), y1 = cy + r * Math.sin(rad(a0 + 90));
    return `<path d="M${f(cx)} ${f(cy)} L${f(x0)} ${f(y0)} A${r} ${r} 0 0 1 ${f(x1)} ${f(y1)} Z" fill="${COL[col].base}"/>`;
  };
  return `<circle cx="${cx}" cy="${cy}" r="${r + ow / 2}" fill="${outline}"/>` +
    q(180, 'red') + q(270, 'blue') + q(0, 'green') + q(90, 'yellow');
}

// ---------- cards (300 x 420) ----------
const CW = 300, CH = 420;

function cardFrame(col, defsOut) {
  const g = id('g'), sheen = id('s');
  defsOut.push(`<linearGradient id="${g}" x1="0" y1="0" x2="0.35" y2="1"><stop offset="0" stop-color="${col.light}"/><stop offset="0.55" stop-color="${col.base}"/><stop offset="1" stop-color="${col.base}"/></linearGradient>`);
  defsOut.push(`<linearGradient id="${sheen}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#fff" stop-opacity="0.22"/><stop offset="0.5" stop-color="#fff" stop-opacity="0"/></linearGradient>`);
  return rrect(1, 1, CW - 2, CH - 2, 30, `fill="#ffffff"`) +
    rrect(3, 3, CW - 6, CH - 6, 28, `fill="none" stroke="#000" stroke-opacity="0.10" stroke-width="2"`) +
    rrect(15, 15, CW - 30, CH - 30, 20, `fill="url(#${g})"`) +
    rrect(15, 15, CW - 30, CH - 30, 20, `fill="url(#${sheen})"`) +
    rrect(15, 15, CW - 30, CH - 30, 20, `fill="none" stroke="#000" stroke-opacity="0.12" stroke-width="3"`);
}

const LEAF = leafPath(52, 82, 196, 256, 26, 98, 26, 98);

function corners(content) {
  // content(x, y) draws one corner index; the second is rotated 180 degrees.
  return content(56, 64) + `<g transform="rotate(180 ${CW / 2} ${CH / 2})">${content(56, 64)}</g>`;
}

function numberCard(colKey, v) {
  const col = COL[colKey], defs = [];
  let body = cardFrame(col, defs);
  body += `<path d="${LEAF}" fill="#000" opacity="0.18" transform="translate(4 7)"/>`;
  body += `<path d="${LEAF}" fill="#fff"/>`;
  const s = String(v);
  const under = (v === 6 || v === 9);
  // centre numeral with a soft shadow
  body += text(156, 222, 210, s, INK, INK, 14, `opacity="0.25" transform="translate(5 8)"`);
  body += text(152, 214, 210, s, col.base, INK, 12);
  if (under) body += rrect(104, 300, 92, 16, 8, `fill="${col.base}" stroke="${INK}" stroke-width="6"`);
  body += corners((x, y) => text(x, y, 76, s, '#fff', INK, 9) + (under ? rrect(x - 22, y + 38, 40, 9, 4.5, `fill="#fff" stroke="${INK}" stroke-width="4"`) : ''));
  return svg(CW, CH, body, defs.join(''));
}

function actionCard(colKey, v) {
  const col = COL[colKey], defs = [];
  let body = cardFrame(col, defs);
  body += `<path d="${LEAF}" fill="#000" opacity="0.18" transform="translate(4 7)"/>`;
  body += `<path d="${LEAF}" fill="#fff"/>`;
  if (v === 10) {
    body += `<g opacity="0.22" transform="translate(5 8)">${skipIcon(150, 210, 62, 26, INK, INK, 12)}</g>`;
    body += skipIcon(150, 210, 62, 26, col.base, INK, 12);
    body += corners((x, y) => skipIcon(x, y, 22, 10, '#fff', INK, 7));
  } else if (v === 11) {
    body += `<g opacity="0.22" transform="translate(5 8)">${reverseIcon(150, 210, 62, 24, INK, INK, 12)}</g>`;
    body += reverseIcon(150, 210, 62, 24, col.base, INK, 12);
    body += corners((x, y) => reverseIcon(x, y, 22, 9, '#fff', INK, 6));
  } else {
    body += `<g opacity="0.22" transform="translate(5 8)">${miniCard(128, 196, 82, 116, -14, INK, INK, 9)}${miniCard(172, 226, 82, 116, 10, INK, INK, 9)}</g>`;
    body += miniCard(128, 196, 82, 116, -14, col.base, INK, 9);
    body += miniCard(172, 226, 82, 116, 10, col.base, INK, 9);
    body += corners((x, y) => text(x + 6, y, 62, '+2', '#fff', INK, 9));
  }
  return svg(CW, CH, body, defs.join(''));
}

function wildCard(plus4) {
  const col = COL.wild, defs = [];
  let body = cardFrame(col, defs);
  const clip = id('c');
  defs.push(`<clipPath id="${clip}"><path d="${LEAF}"/></clipPath>`);
  body += `<path d="${LEAF}" fill="#000" opacity="0.3" transform="translate(4 7)"/>`;
  if (!plus4) {
    body += `<g clip-path="url(#${clip})">` +
      `<rect x="40" y="70" width="110" height="140" fill="${COL.red.base}"/>` +
      `<rect x="150" y="70" width="110" height="140" fill="${COL.blue.base}"/>` +
      `<rect x="40" y="210" width="110" height="140" fill="${COL.yellow.base}"/>` +
      `<rect x="150" y="210" width="110" height="140" fill="${COL.green.base}"/>` +
      `<rect x="40" y="70" width="220" height="140" fill="#fff" opacity="0.12"/></g>`;
    body += `<path d="${LEAF}" fill="none" stroke="#fff" stroke-width="10"/>`;
    body += `<line x1="150" y1="82" x2="150" y2="338" stroke="#fff" stroke-width="6"/><line x1="52" y1="210" x2="248" y2="210" stroke="#fff" stroke-width="6"/>`;
    body += corners((x, y) => pie(x, y, 20, '#fff', 6));
  } else {
    body += `<path d="${LEAF}" fill="${col.light}" opacity="0.55"/>`;
    const cards = [['red', 104, 186, -16], ['yellow', 134, 226, -6], ['green', 166, 196, 8], ['blue', 196, 232, 18]];
    // arrange as a little fan / cross like the reference
    body += `<g opacity="0.25" transform="translate(5 8)">` + cards.map(c => miniCard(c[1], c[2], 70, 100, c[3], INK, INK, 8)).join('') + `</g>`;
    body += cards.map(c => miniCard(c[1], c[2], 70, 100, c[3], COL[c[0]].base, INK, 8)).join('');
    body += corners((x, y) => text(x + 6, y, 62, '+4', '#fff', INK, 9));
  }
  return svg(CW, CH, body, defs.join(''));
}

function cardBack() {
  const defs = [], g = id('g'), pat = id('p'), rg = id('r');
  defs.push(`<radialGradient id="${g}" cx="0.5" cy="0.45" r="0.75"><stop offset="0" stop-color="#4A3478"/><stop offset="1" stop-color="#1C1331"/></radialGradient>`);
  defs.push(`<pattern id="${pat}" width="26" height="26" patternUnits="userSpaceOnUse" patternTransform="rotate(45)"><rect width="26" height="26" fill="none"/><rect width="13" height="13" fill="#fff" opacity="0.05"/></pattern>`);
  defs.push(`<linearGradient id="${rg}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FF5A4E"/><stop offset="1" stop-color="#D0202D"/></linearGradient>`);
  let body = rrect(1, 1, CW - 2, CH - 2, 30, `fill="#fff"`) +
    rrect(3, 3, CW - 6, CH - 6, 28, `fill="none" stroke="#000" stroke-opacity="0.10" stroke-width="2"`) +
    rrect(15, 15, CW - 30, CH - 30, 20, `fill="url(#${g})"`) +
    rrect(15, 15, CW - 30, CH - 30, 20, `fill="url(#${pat})"`) +
    rrect(26, 26, CW - 52, CH - 52, 14, `fill="none" stroke="#fff" stroke-opacity="0.10" stroke-width="3"`);
  body += `<g transform="rotate(-18 150 210)">` +
    `<path d="${leafPath(58, 112, 184, 196, 30, 92, 30, 92)}" fill="#000" opacity="0.3" transform="translate(4 7)"/>` +
    `<path d="${leafPath(58, 112, 184, 196, 30, 92, 30, 92)}" fill="url(#${rg})" stroke="#fff" stroke-width="7"/>` +
    text(152, 212, 92, 'UNO', '#FFD84A', INK, 12) + `</g>`;
  return svg(CW, CH, body, defs.join(''));
}

function cardSilhouette(fill, blur, pad, opacity, scale = 1) {
  const w = CW + pad * 2, h = CH + pad * 2, fl = id('f');
  const sw = CW * scale, sh = CH * scale;
  const defs = blur ? `<filter id="${fl}" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="${blur}"/></filter>` : '';
  return svg(w, h, rrect((w - sw) / 2, (h - sh) / 2, sw, sh, 30 * scale, `fill="${fill}" opacity="${opacity}" ${blur ? `filter="url(#${fl})"` : ''}`), defs);
}

// ---------- UI pieces ----------

function roundRect(w, h, r, fill = '#fff') {
  return svg(w, h, rrect(0, 0, w, h, r, `fill="${fill}"`));
}

function glossyButton(base, light, dark) {
  const w = 300, h = 140, g = id('g'), hl = id('h');
  const defs = `<linearGradient id="${g}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${light}"/><stop offset="1" stop-color="${base}"/></linearGradient>` +
    `<linearGradient id="${hl}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity="0.55"/><stop offset="1" stop-color="#fff" stop-opacity="0.05"/></linearGradient>`;
  const body = rrect(2, 14, w - 4, h - 16, 62, `fill="${dark}"`) +
    rrect(2, 2, w - 4, h - 16, 62, `fill="url(#${g})"`) +
    rrect(18, 8, w - 36, 46, 23, `fill="url(#${hl})"`) +
    rrect(2, 2, w - 4, h - 16, 62, `fill="none" stroke="#fff" stroke-opacity="0.35" stroke-width="3"`);
  return svg(w, h, body, defs);
}

function panel() {
  const w = 256, h = 256, g = id('g');
  const defs = `<linearGradient id="${g}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#2E3B40"/><stop offset="1" stop-color="#1D2629"/></linearGradient>`;
  return svg(w, h, rrect(2, 2, w - 4, h - 4, 46, `fill="url(#${g})" stroke="#fff" stroke-opacity="0.10" stroke-width="3"`), defs);
}

function circle(size, fill = '#fff') {
  return svg(size, size, `<circle cx="${size / 2}" cy="${size / 2}" r="${size / 2 - 1}" fill="${fill}"/>`);
}

function ring(size, width) {
  return svg(size, size, `<circle cx="${size / 2}" cy="${size / 2}" r="${size / 2 - width / 2 - 1}" fill="none" stroke="#fff" stroke-width="${width}"/>`);
}

function softGlow(size) {
  const g = id('g');
  return svg(size, size, `<circle cx="${size / 2}" cy="${size / 2}" r="${size / 2}" fill="url(#${g})"/>`,
    `<radialGradient id="${g}"><stop offset="0" stop-color="#fff" stop-opacity="1"/><stop offset="0.35" stop-color="#fff" stop-opacity="0.55"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>`);
}

function colorBall(colKey) {
  const c = COL[colKey], s = 220, g = id('g'), h = id('h');
  const defs = `<radialGradient id="${g}" cx="0.38" cy="0.32" r="0.75"><stop offset="0" stop-color="${c.light}"/><stop offset="0.55" stop-color="${c.base}"/><stop offset="1" stop-color="${c.dark}"/></radialGradient>` +
    `<linearGradient id="${h}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity="0.85"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient>`;
  return svg(s, s, `<circle cx="110" cy="116" r="100" fill="#000" opacity="0.28"/>` +
    `<circle cx="110" cy="108" r="100" fill="url(#${g})" stroke="${c.dark}" stroke-width="4"/>` +
    `<ellipse cx="104" cy="58" rx="62" ry="34" fill="url(#${h})"/>` +
    `<circle cx="150" cy="150" r="10" fill="#fff" opacity="0.25"/>`, defs);
}

// White icons on a 128 grid, tinted in Unity.
function icon(body, extraDefs = '') {
  return svg(128, 128, `<g fill="none" stroke="#fff" stroke-width="13" stroke-linecap="round" stroke-linejoin="round">${body}</g>`, extraDefs);
}

function gearPath(cx, cy, r1, r2, teeth) {
  const pts = [];
  for (let i = 0; i < teeth * 2; i++) {
    const a0 = (i / (teeth * 2)) * Math.PI * 2, a1 = ((i + 1) / (teeth * 2)) * Math.PI * 2;
    const r = i % 2 === 0 ? r2 : r1;
    pts.push([cx + r * Math.cos(a0 + 0.08), cy + r * Math.sin(a0 + 0.08)], [cx + r * Math.cos(a1 - 0.08), cy + r * Math.sin(a1 - 0.08)]);
  }
  return 'M' + pts.map(p => `${f(p[0])} ${f(p[1])}`).join(' L') + ' Z';
}

function starPath(cx, cy, r1, r2, n, rot = -90) {
  const pts = [];
  for (let i = 0; i < n * 2; i++) {
    const a = (rot + i * 180 / n) * Math.PI / 180, r = i % 2 === 0 ? r1 : r2;
    pts.push(`${f(cx + r * Math.cos(a))} ${f(cy + r * Math.sin(a))}`);
  }
  return 'M' + pts.join(' L') + ' Z';
}

const ICONS = {
  back: icon(`<path d="M76 30 L42 64 L76 98" stroke-width="15"/>`),
  dots: svg(128, 128, `<circle cx="32" cy="64" r="11" fill="#fff"/><circle cx="64" cy="64" r="11" fill="#fff"/><circle cx="96" cy="64" r="11" fill="#fff"/>`),
  close: icon(`<path d="M38 38 L90 90 M90 38 L38 90" stroke-width="15"/>`),
  sound_on: icon(`<path d="M22 50 L42 50 L66 28 L66 100 L42 78 L22 78 Z" fill="#fff" stroke-width="8"/><path d="M82 46 Q94 64 82 82"/><path d="M94 32 Q118 64 94 96"/>`),
  sound_off: icon(`<path d="M22 50 L42 50 L66 28 L66 100 L42 78 L22 78 Z" fill="#fff" stroke-width="8"/><path d="M84 48 L112 80 M112 48 L84 80" stroke-width="12"/>`),
  home: icon(`<path d="M20 62 L64 24 L108 62"/><path d="M34 54 L34 104 L94 104 L94 54" /><path d="M56 104 L56 78 L72 78 L72 104" stroke-width="10"/>`),
  replay: icon(`<path d="M100 64 A36 36 0 1 1 86 35"/><path d="M70 20 L92 34 L76 54" stroke-width="12"/>`),
  gear: svg(128, 128, `<path d="${gearPath(64, 64, 40, 54, 8)} M64 47 A17 17 0 1 0 64 81 A17 17 0 1 0 64 47 Z" fill="#fff" fill-rule="evenodd"/>`),
  users: svg(128, 128, `<circle cx="48" cy="44" r="18" fill="#fff"/><path d="M14 104 Q14 72 48 70 Q82 72 82 104 Z" fill="#fff"/>` +
    `<circle cx="88" cy="40" r="15" fill="#fff" opacity="0.7"/><path d="M88 64 Q116 66 116 96 L90 96 Q90 76 76 68 Q80 64 88 64 Z" fill="#fff" opacity="0.7"/>`),
  trophy: svg(128, 128, `<path d="M36 18 L92 18 L92 50 Q92 78 64 82 Q36 78 36 50 Z" fill="#fff"/><path d="M36 28 L18 28 Q16 54 38 58 M92 28 L110 28 Q112 54 90 58" fill="none" stroke="#fff" stroke-width="9" stroke-linecap="round"/>` +
    `<rect x="56" y="80" width="16" height="16" fill="#fff"/><rect x="38" y="96" width="52" height="14" rx="5" fill="#fff"/>`),
  star: svg(128, 128, `<path d="${starPath(64, 66, 54, 24, 5)}" fill="#fff" stroke="#fff" stroke-width="6" stroke-linejoin="round"/>`),
  clock: icon(`<circle cx="64" cy="64" r="44" stroke-width="11"/><path d="M64 38 L64 66 L82 76" stroke-width="10"/>`),
  check: icon(`<path d="M28 66 L54 92 L102 38" stroke-width="16"/>`),
  play: svg(128, 128, `<path d="M42 26 L104 64 L42 102 Z" fill="#fff" stroke="#fff" stroke-width="10" stroke-linejoin="round"/>`),
  edit: icon(`<path d="M84 24 L104 44 L52 96 L28 100 L32 76 Z" stroke-width="10"/><path d="M74 34 L94 54" stroke-width="8"/>`),
  bolt: svg(128, 128, `<path d="M74 8 L28 72 L60 72 L50 120 L100 52 L66 52 Z" fill="#fff" stroke="#fff" stroke-width="5" stroke-linejoin="round"/>`),
  arrow_dir: svg(160, 160, `<g fill="none" stroke="#fff" stroke-width="22" stroke-linecap="round"><path d="M34 112 A62 62 0 0 1 104 46"/></g><path d="M96 18 L140 50 L96 80 Z" fill="#fff" stroke="#fff" stroke-width="8" stroke-linejoin="round"/>`),
  sparkle: svg(128, 128, `<path d="M64 4 Q70 58 124 64 Q70 70 64 124 Q58 70 4 64 Q58 58 64 4 Z" fill="#fff"/>`),
  dot: circle(48),
};

function coin() {
  const g = id('g');
  return svg(128, 128, `<circle cx="64" cy="68" r="54" fill="#B26B00"/><circle cx="64" cy="62" r="54" fill="url(#${g})" stroke="#9C5A00" stroke-width="4"/>` +
    `<circle cx="64" cy="62" r="38" fill="none" stroke="#FFF3B0" stroke-width="5" opacity="0.8"/><path d="${starPath(64, 63, 24, 10, 5)}" fill="#FFF3B0"/>`,
    `<radialGradient id="${g}" cx="0.4" cy="0.3" r="0.8"><stop offset="0" stop-color="#FFE57A"/><stop offset="1" stop-color="#F2A300"/></radialGradient>`);
}

function crown() {
  const g = id('g');
  return svg(160, 128, `<path d="M18 40 L50 70 L80 22 L110 70 L142 40 L128 104 L32 104 Z" fill="url(#${g})" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>` +
    `<circle cx="18" cy="38" r="10" fill="#FFE57A" stroke="${INK}" stroke-width="5"/><circle cx="80" cy="20" r="10" fill="#FFE57A" stroke="${INK}" stroke-width="5"/><circle cx="142" cy="38" r="10" fill="#FFE57A" stroke="${INK}" stroke-width="5"/>`,
    `<linearGradient id="${g}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFE57A"/><stop offset="1" stop-color="#F2A300"/></linearGradient>`);
}

function burst(w, h, points, outerScale, innerScale, fillA, fillB, label, size) {
  const g = id('g');
  const cx = w / 2, cy = h / 2;
  const pts = [];
  for (let i = 0; i < points * 2; i++) {
    const a = (i / (points * 2)) * Math.PI * 2 - Math.PI / 2;
    const rx = (i % 2 === 0 ? outerScale : innerScale) * w / 2, ry = (i % 2 === 0 ? outerScale : innerScale) * h / 2;
    pts.push(`${f(cx + rx * Math.cos(a))} ${f(cy + ry * Math.sin(a))}`);
  }
  const d = 'M' + pts.join(' L') + ' Z';
  return svg(w, h, `<path d="${d}" fill="#000" opacity="0.3" transform="translate(6 9)"/>` +
    `<path d="${d}" fill="url(#${g})" stroke="${INK}" stroke-width="9" stroke-linejoin="round"/>` +
    `<g transform="rotate(-8 ${cx} ${cy})">` + text(cx + 4, cy + 6, size, label, INK, INK, 26, 'opacity="0.35"') +
    text(cx, cy, size, label, '#FFE04A', INK, 20) + text(cx, cy, size, label, '#FFE04A', '#fff', 6) + `</g>`,
    `<radialGradient id="${g}" cx="0.5" cy="0.45" r="0.6"><stop offset="0" stop-color="${fillA}"/><stop offset="1" stop-color="${fillB}"/></radialGradient>`);
}

function vsBadge() {
  const g = id('g');
  return svg(340, 240, `<g transform="skewX(-8)">` +
    text(186, 126, 190, 'VS', INK, INK, 34, 'opacity="0.4"') +
    text(180, 116, 190, 'VS', `url(#${g})`, INK, 26) + text(180, 116, 190, 'VS', `url(#${g})`, '#fff', 8) + `</g>`,
    `<linearGradient id="${g}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFF07A"/><stop offset="1" stop-color="#FF8A00"/></linearGradient>`);
}

function rays(size, count) {
  let body = '';
  const c = size / 2;
  for (let i = 0; i < count; i++) {
    const a0 = (i / count) * Math.PI * 2, a1 = a0 + Math.PI / count;
    body += `<path d="M${c} ${c} L${f(c + c * Math.cos(a0))} ${f(c + c * Math.sin(a0))} L${f(c + c * Math.cos(a1))} ${f(c + c * Math.sin(a1))} Z" fill="#fff"/>`;
  }
  const m = id('m'), g = id('g');
  return svg(size, size, `<g mask="url(#${m})">${body}</g>`,
    `<radialGradient id="${g}"><stop offset="0" stop-color="#fff" stop-opacity="1"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>` +
    `<mask id="${m}"><circle cx="${c}" cy="${c}" r="${c}" fill="url(#${g})"/></mask>`);
}

function ribbon(fill, dark) {
  const w = 760, h = 190;
  return svg(w, h, `<path d="M10 70 L110 70 L110 170 L60 150 L10 170 L40 120 Z" fill="${dark}"/>` +
    `<path d="M750 70 L650 70 L650 170 L700 150 L750 170 L720 120 Z" fill="${dark}"/>` +
    `<path d="M90 30 Q380 -6 670 30 L670 150 Q380 114 90 150 Z" fill="#000" opacity="0.25" transform="translate(0 8)"/>` +
    `<path d="M90 30 Q380 -6 670 30 L670 150 Q380 114 90 150 Z" fill="${fill}" stroke="${INK}" stroke-width="6"/>` +
    `<path d="M104 42 Q380 8 656 42 L656 70 Q380 38 104 70 Z" fill="#fff" opacity="0.22"/>`);
}

function bubble() {
  return svg(200, 200, `<path d="M100 10 C152 10 188 42 188 88 C188 134 152 164 100 164 C88 164 78 162 68 160 L38 188 L44 152 C24 138 12 116 12 88 C12 42 48 10 100 10 Z" fill="#fff"/>`);
}

function fxSkip() { return svg(256, 256, skipIcon(128, 128, 84, 34, '#fff', INK, 16)); }
function fxReverse() { return svg(256, 256, reverseIcon(128, 128, 84, 32, '#fff', INK, 16)); }

// ---------- avatars (256 x 256) ----------

function eyes(lx, rx, y, r, opts = {}) {
  const pupil = r * (opts.pupil || 0.55);
  const one = x => `<circle cx="${x}" cy="${y}" r="${r}" fill="#fff" stroke="${INK}" stroke-width="5"/>` +
    `<circle cx="${x + r * 0.12}" cy="${y + r * 0.1}" r="${pupil}" fill="${INK}"/>` +
    `<circle cx="${x + r * 0.32}" cy="${y - r * 0.22}" r="${pupil * 0.36}" fill="#fff"/>`;
  return one(lx) + one(rx);
}
function cheeks(lx, rx, y, r = 13, c = '#FF7B8A') {
  return `<ellipse cx="${lx}" cy="${y}" rx="${r}" ry="${r * 0.62}" fill="${c}" opacity="0.6"/><ellipse cx="${rx}" cy="${y}" rx="${r}" ry="${r * 0.62}" fill="${c}" opacity="0.6"/>`;
}
function smile(cx, y, w, open = false) {
  if (open) return `<path d="M${cx - w} ${y} Q${cx} ${y + w * 1.3} ${cx + w} ${y} Z" fill="#7A1E2C" stroke="${INK}" stroke-width="5" stroke-linejoin="round"/><path d="M${cx - w * 0.5} ${y + w * 0.62} Q${cx} ${y + w * 0.4} ${cx + w * 0.5} ${y + w * 0.62} Q${cx} ${y + w * 0.98} ${cx - w * 0.5} ${y + w * 0.62} Z" fill="#FF6F86"/>`;
  return `<path d="M${cx - w} ${y} Q${cx} ${y + w * 0.9} ${cx + w} ${y}" fill="none" stroke="${INK}" stroke-width="6" stroke-linecap="round"/>`;
}

function avatarBase(bgA, bgB, inner) {
  const g = id('g'), cl = id('c');
  return svg(256, 256,
    `<circle cx="128" cy="128" r="127" fill="url(#${g})"/>` +
    `<g clip-path="url(#${cl})">${inner}</g>` +
    `<circle cx="128" cy="128" r="124" fill="none" stroke="#fff" stroke-opacity="0.18" stroke-width="5"/>`,
    `<radialGradient id="${g}" cx="0.5" cy="0.35" r="0.75"><stop offset="0" stop-color="${bgA}"/><stop offset="1" stop-color="${bgB}"/></radialGradient>` +
    `<clipPath id="${cl}"><circle cx="128" cy="128" r="127"/></clipPath>`);
}

const S = `stroke="${INK}" stroke-width="6" stroke-linejoin="round"`;
const AVATARS = [
  // 0 cat
  avatarBase('#FFB36B', '#E36414',
    `<path d="M62 92 L58 30 L104 64 Z" fill="#F2F2F2" ${S}/><path d="M194 92 L198 30 L152 64 Z" fill="#F2F2F2" ${S}/>` +
    `<path d="M70 82 L67 46 L94 66 Z" fill="#FF9FB0"/><path d="M186 82 L189 46 L162 66 Z" fill="#FF9FB0"/>` +
    `<ellipse cx="128" cy="150" rx="88" ry="78" fill="#F2F2F2" ${S}/>` + eyes(96, 160, 140, 20) + cheeks(78, 178, 172) +
    `<path d="M120 164 L136 164 L128 174 Z" fill="#FF7B8A" ${S}/>` + `<path d="M128 174 Q118 190 106 182 M128 174 Q138 190 150 182" fill="none" stroke="${INK}" stroke-width="5" stroke-linecap="round"/>` +
    `<path d="M40 160 L80 164 M40 178 L80 174 M216 160 L176 164 M216 178 L176 174" stroke="${INK}" stroke-width="4" stroke-linecap="round"/>`),
  // 1 bear
  avatarBase('#9FD3FF', '#2D7DD2',
    `<circle cx="66" cy="74" r="30" fill="#A0673C" ${S}/><circle cx="190" cy="74" r="30" fill="#A0673C" ${S}/><circle cx="66" cy="74" r="14" fill="#E8B48A"/><circle cx="190" cy="74" r="14" fill="#E8B48A"/>` +
    `<ellipse cx="128" cy="148" rx="90" ry="84" fill="#A0673C" ${S}/>` + eyes(96, 160, 132, 17) +
    `<ellipse cx="128" cy="178" rx="40" ry="30" fill="#E8B48A" ${S}/><ellipse cx="128" cy="166" rx="14" ry="10" fill="${INK}"/>` + smile(128, 182, 16) + cheeks(70, 186, 162)),
  // 2 panda
  avatarBase('#B8F2C2', '#2EA44F',
    `<circle cx="64" cy="70" r="30" fill="${INK}"/><circle cx="192" cy="70" r="30" fill="${INK}"/>` +
    `<ellipse cx="128" cy="148" rx="92" ry="84" fill="#FAFAFA" ${S}/>` +
    `<ellipse cx="94" cy="138" rx="26" ry="32" transform="rotate(-24 94 138)" fill="${INK}"/><ellipse cx="162" cy="138" rx="26" ry="32" transform="rotate(24 162 138)" fill="${INK}"/>` +
    `<circle cx="98" cy="136" r="12" fill="#fff"/><circle cx="158" cy="136" r="12" fill="#fff"/><circle cx="100" cy="137" r="7" fill="${INK}"/><circle cx="156" cy="137" r="7" fill="${INK}"/>` +
    `<ellipse cx="128" cy="170" rx="13" ry="9" fill="${INK}"/>` + smile(128, 182, 14) + cheeks(72, 184, 176)),
  // 3 fox
  avatarBase('#FFE29A', '#F2A541',
    `<path d="M50 112 L44 26 L110 74 Z" fill="#F26B1D" ${S}/><path d="M206 112 L212 26 L146 74 Z" fill="#F26B1D" ${S}/>` +
    `<path d="M60 92 L58 46 L94 72 Z" fill="${INK}" opacity="0.8"/><path d="M196 92 L198 46 L162 72 Z" fill="${INK}" opacity="0.8"/>` +
    `<path d="M128 228 L40 140 Q40 70 128 66 Q216 70 216 140 Z" fill="#F26B1D" ${S}/>` +
    `<path d="M128 228 L62 160 Q100 150 128 186 Q156 150 194 160 Z" fill="#FFF4E6"/>` + eyes(96, 160, 128, 16) +
    `<ellipse cx="128" cy="196" rx="12" ry="9" fill="${INK}"/>`),
  // 4 frog
  avatarBase('#C7B8FF', '#6A4CE0',
    `<ellipse cx="128" cy="160" rx="100" ry="72" fill="#5BC236" ${S}/>` +
    `<circle cx="80" cy="96" r="36" fill="#5BC236" ${S}/><circle cx="176" cy="96" r="36" fill="#5BC236" ${S}/>` + eyes(80, 176, 96, 22, { pupil: 0.5 }) +
    `<path d="M70 168 Q128 214 186 168" fill="none" stroke="${INK}" stroke-width="7" stroke-linecap="round"/>` + cheeks(60, 196, 160, 14, '#FF8FA0') +
    `<circle cx="116" cy="146" r="4" fill="${INK}"/><circle cx="140" cy="146" r="4" fill="${INK}"/>`),
  // 5 robot
  avatarBase('#FFB3C7', '#E0457B',
    `<line x1="128" y1="52" x2="128" y2="26" stroke="${INK}" stroke-width="7"/><circle cx="128" cy="24" r="12" fill="#FFD23F" ${S}/>` +
    `<rect x="40" y="56" width="176" height="150" rx="34" fill="#B9C6D2" ${S}/><rect x="58" y="84" width="140" height="74" rx="22" fill="#24324A" ${S}/>` +
    `<circle cx="98" cy="120" r="16" fill="#46E2FF"/><circle cx="158" cy="120" r="16" fill="#46E2FF"/><circle cx="102" cy="116" r="6" fill="#fff"/><circle cx="162" cy="116" r="6" fill="#fff"/>` +
    `<rect x="94" y="172" width="68" height="18" rx="9" fill="#24324A"/><path d="M104 172 L104 190 M118 172 L118 190 M132 172 L132 190 M146 172 L146 190" stroke="#46E2FF" stroke-width="4"/>` +
    `<rect x="22" y="104" width="22" height="46" rx="8" fill="#8B9AA8" ${S}/><rect x="212" y="104" width="22" height="46" rx="8" fill="#8B9AA8" ${S}/>`),
  // 6 alien
  avatarBase('#A8F0FF', '#159FC1',
    `<path d="M128 40 C200 40 222 100 212 148 C202 196 166 226 128 226 C90 226 54 196 44 148 C34 100 56 40 128 40 Z" fill="#9BE564" ${S}/>` +
    `<ellipse cx="90" cy="130" rx="30" ry="22" transform="rotate(22 90 130)" fill="${INK}"/><ellipse cx="166" cy="130" rx="30" ry="22" transform="rotate(-22 166 130)" fill="${INK}"/>` +
    `<ellipse cx="98" cy="122" rx="9" ry="6" fill="#fff"/><ellipse cx="158" cy="122" rx="9" ry="6" fill="#fff"/>` + smile(128, 182, 18) +
    `<circle cx="128" cy="80" r="14" fill="#fff" ${S}/><circle cx="130" cy="82" r="7" fill="${INK}"/>`),
  // 7 owl
  avatarBase('#FFD6A5', '#F08A4B',
    `<path d="M52 70 L72 40 L96 64 Z M204 70 L184 40 L160 64 Z" fill="#7A4E2D" ${S}/>` +
    `<ellipse cx="128" cy="150" rx="92" ry="88" fill="#7A4E2D" ${S}/><ellipse cx="128" cy="176" rx="60" ry="54" fill="#E9C9A0"/>` +
    `<circle cx="92" cy="120" r="34" fill="#F7E7CE" ${S}/><circle cx="164" cy="120" r="34" fill="#F7E7CE" ${S}/>` + eyes(92, 164, 120, 18, { pupil: 0.62 }) +
    `<path d="M116 146 L140 146 L128 168 Z" fill="#FFB020" ${S}/>`),
  // 8 penguin
  avatarBase('#D6E4FF', '#5B7CFA',
    `<ellipse cx="128" cy="150" rx="94" ry="90" fill="#26304A" ${S}/><path d="M128 92 C176 92 196 130 192 168 C188 206 160 226 128 226 C96 226 68 206 64 168 C60 130 80 92 128 92 Z" fill="#FAFAFA"/>` +
    eyes(100, 156, 134, 15) + `<path d="M110 160 L146 160 L128 180 Z" fill="#FFA41B" ${S}/>` + cheeks(80, 176, 170, 12)),
  // 9 monkey
  avatarBase('#FFF3A8', '#E8C42A',
    `<circle cx="46" cy="132" r="30" fill="#8D5B3E" ${S}/><circle cx="210" cy="132" r="30" fill="#8D5B3E" ${S}/><circle cx="46" cy="132" r="15" fill="#F2C9A0"/><circle cx="210" cy="132" r="15" fill="#F2C9A0"/>` +
    `<ellipse cx="128" cy="140" rx="84" ry="88" fill="#8D5B3E" ${S}/>` +
    `<path d="M128 104 C150 84 192 92 190 130 C188 150 172 156 162 158 C176 172 176 210 128 214 C80 210 80 172 94 158 C84 156 68 150 66 130 C64 92 106 84 128 104 Z" fill="#F2C9A0"/>` +
    eyes(102, 154, 128, 14) + `<circle cx="120" cy="164" r="4" fill="${INK}"/><circle cx="136" cy="164" r="4" fill="${INK}"/>` + smile(128, 180, 22, true)),
  // 10 tiger
  avatarBase('#FFC2D1', '#D6336C',
    `<circle cx="66" cy="70" r="26" fill="#FF9F1C" ${S}/><circle cx="190" cy="70" r="26" fill="#FF9F1C" ${S}/><circle cx="66" cy="70" r="11" fill="#FFE8CC"/><circle cx="190" cy="70" r="11" fill="#FFE8CC"/>` +
    `<ellipse cx="128" cy="148" rx="92" ry="84" fill="#FF9F1C" ${S}/>` +
    `<path d="M128 66 L118 92 L128 100 L138 92 Z M52 120 L82 130 L56 140 Z M204 120 L174 130 L200 140 Z M60 168 L88 164 L66 182 Z M196 168 L168 164 L190 182 Z" fill="${INK}"/>` +
    `<ellipse cx="128" cy="184" rx="44" ry="32" fill="#FFF4E6"/>` + eyes(96, 160, 136, 16) +
    `<path d="M118 168 L138 168 L128 180 Z" fill="#FF7B8A" ${S}/>` + smile(128, 186, 12)),
  // 11 bunny
  avatarBase('#C9F2E9', '#20B2AA',
    `<ellipse cx="94" cy="56" rx="22" ry="56" transform="rotate(-12 94 56)" fill="#F7F1F5" ${S}/><ellipse cx="162" cy="56" rx="22" ry="56" transform="rotate(12 162 56)" fill="#F7F1F5" ${S}/>` +
    `<ellipse cx="94" cy="60" rx="10" ry="38" transform="rotate(-12 94 60)" fill="#FFB3C7"/><ellipse cx="162" cy="60" rx="10" ry="38" transform="rotate(12 162 60)" fill="#FFB3C7"/>` +
    `<ellipse cx="128" cy="160" rx="88" ry="76" fill="#F7F1F5" ${S}/>` + eyes(98, 158, 150, 17) + cheeks(76, 180, 182) +
    `<path d="M120 176 L136 176 L128 186 Z" fill="#FF7B8A" ${S}/><rect x="120" y="194" width="16" height="16" rx="3" fill="#fff" ${S}/>`),
];

// ---------- emotes (160 x 160) ----------

function face(fillA, fillB, inner) {
  const g = id('g');
  return svg(160, 160, `<circle cx="80" cy="84" r="72" fill="#000" opacity="0.25"/><circle cx="80" cy="78" r="72" fill="url(#${g})" stroke="${INK}" stroke-width="6"/>${inner}`,
    `<radialGradient id="${g}" cx="0.4" cy="0.3" r="0.8"><stop offset="0" stop-color="${fillA}"/><stop offset="1" stop-color="${fillB}"/></radialGradient>`);
}
const Y1 = '#FFE873', Y2 = '#FFB300';
const EMOTES = [
  face(Y1, Y2, `<path d="M46 66 Q56 54 66 66 M94 66 Q104 54 114 66" fill="none" stroke="${INK}" stroke-width="7" stroke-linecap="round"/>` +
    `<path d="M42 90 Q80 140 118 90 Z" fill="#7A1E2C" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/><path d="M58 112 Q80 100 102 112 Q80 132 58 112 Z" fill="#FF6F86"/>` + cheeks(38, 122, 92, 10)),
  face(Y1, Y2, `<path d="M42 58 L62 66 L42 74 M118 58 L98 66 L118 74" fill="none" stroke="${INK}" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/>` +
    `<path d="M38 86 Q80 150 122 86 Z" fill="#7A1E2C" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/><path d="M56 116 Q80 102 104 116 Q80 138 56 116 Z" fill="#FF6F86"/>` +
    `<path d="M24 74 Q14 96 26 104 Q38 96 24 74 Z M136 74 Q146 96 134 104 Q122 96 136 74 Z" fill="#4FC3F7" stroke="${INK}" stroke-width="4"/>`),
  face(Y1, Y2, `<path d="M28 58 L132 58 L126 76 Q118 92 100 90 Q86 88 82 72 L78 72 Q74 88 60 90 Q42 92 34 76 Z" fill="${INK}"/>` +
    `<path d="M40 64 L58 64 M96 64 L114 64" stroke="#fff" stroke-width="4" stroke-linecap="round" opacity="0.6"/>` +
    `<path d="M56 110 Q86 126 108 104" fill="none" stroke="${INK}" stroke-width="7" stroke-linecap="round"/>`),
  face(Y1, Y2, `<circle cx="56" cy="64" r="13" fill="#fff" stroke="${INK}" stroke-width="5"/><circle cx="104" cy="64" r="13" fill="#fff" stroke="${INK}" stroke-width="5"/>` +
    `<circle cx="56" cy="66" r="6" fill="${INK}"/><circle cx="104" cy="66" r="6" fill="${INK}"/><ellipse cx="80" cy="110" rx="16" ry="20" fill="#7A1E2C" stroke="${INK}" stroke-width="6"/>` +
    `<path d="M40 44 Q54 36 66 44 M94 44 Q106 36 120 44" fill="none" stroke="${INK}" stroke-width="5" stroke-linecap="round"/>`),
  face('#FF8A65', '#E53935', `<path d="M38 50 L66 62 M122 50 L94 62" stroke="${INK}" stroke-width="8" stroke-linecap="round"/>` +
    `<circle cx="56" cy="74" r="8" fill="${INK}"/><circle cx="104" cy="74" r="8" fill="${INK}"/>` +
    `<path d="M52 118 Q80 96 108 118" fill="none" stroke="${INK}" stroke-width="7" stroke-linecap="round"/>` +
    `<path d="M120 20 L126 34 M134 26 L128 38 M112 30 L124 38" stroke="#fff" stroke-width="5" stroke-linecap="round"/>`),
  face(Y1, Y2, `<path d="M40 58 Q52 50 66 56 M120 58 Q108 50 94 56" fill="none" stroke="${INK}" stroke-width="6" stroke-linecap="round"/>` +
    `<circle cx="56" cy="74" r="8" fill="${INK}"/><circle cx="104" cy="74" r="8" fill="${INK}"/>` +
    `<path d="M54 118 Q80 100 106 118" fill="none" stroke="${INK}" stroke-width="7" stroke-linecap="round"/>` +
    `<path d="M106 84 Q96 106 106 114 Q118 106 106 84 Z" fill="#4FC3F7" stroke="${INK}" stroke-width="4"/>`),
];

// ---------- logo, backgrounds ----------

function logo() {
  const w = 1000, h = 560, defs = [];
  const gl = id('g');
  defs.push(`<linearGradient id="${gl}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFF27A"/><stop offset="0.6" stop-color="#FFC21A"/><stop offset="1" stop-color="#FF8A00"/></linearGradient>`);
  const glow = id('f');
  defs.push(`<filter id="${glow}" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="18"/></filter>`);
  let body = '';
  // fan of cards behind the word
  const fan = [['red', -26], ['yellow', -9], ['green', 9], ['blue', 26]];
  fan.forEach(([c, rot], i) => {
    const cx = 500 + (i - 1.5) * 70, cy = 250;
    body += `<g transform="rotate(${rot} ${cx} ${cy + 260})">` + rrect(cx - 85, cy - 120, 170, 238, 22, `fill="#fff" stroke="${INK}" stroke-width="7"`) +
      rrect(cx - 72, cy - 107, 144, 212, 14, `fill="${COL[c].base}"`) +
      `<path d="${leafPath(cx - 48, cy - 70, 96, 140, 14, 48, 14, 48)}" fill="#fff" opacity="0.9"/></g>`;
  });
  body += `<ellipse cx="500" cy="300" rx="420" ry="150" fill="#000" opacity="0.35" filter="url(#${glow})"/>`;
  // the word
  const word = (dx, dy, fill, stroke, sw, extra = '') => text(500 + dx, 300 + dy, 300, 'UNO', fill, stroke, sw, `letter-spacing="-6" ${extra}`);
  body += `<g transform="rotate(-6 500 300)">` + word(8, 14, INK, INK, 48, 'opacity="0.45"') + word(0, 0, `url(#${gl})`, INK, 40) +
    word(0, 0, `url(#${gl})`, '#fff', 12) + `</g>`;
  // ribbon "GAME"
  body += `<g transform="rotate(-6 500 300)"><path d="M300 400 L700 400 L688 476 L312 476 Z" fill="#000" opacity="0.3" transform="translate(4 8)"/>` +
    `<path d="M300 400 L700 400 L688 476 L312 476 Z" fill="${COL.blue.base}" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>` +
    text(500, 440, 62, 'CARD  GAME', '#fff', INK, 10, 'letter-spacing="6"') + `</g>`;
  return svg(w, h, body, defs.join(''));
}

function background(kind) {
  const w = 1080, h = 1920, defs = [];
  const g = id('g');
  let body = '';
  if (kind === 'table') {
    defs.push(`<radialGradient id="${g}" cx="0.5" cy="0.42" r="0.85"><stop offset="0" stop-color="#3A4C52"/><stop offset="0.6" stop-color="#28363A"/><stop offset="1" stop-color="#172023"/></radialGradient>`);
    const pat = id('p');
    defs.push(`<pattern id="${pat}" width="54" height="54" patternUnits="userSpaceOnUse" patternTransform="rotate(45)"><rect width="27" height="27" fill="#fff" opacity="0.018"/></pattern>`);
    body += `<rect width="${w}" height="${h}" fill="url(#${g})"/><rect width="${w}" height="${h}" fill="url(#${pat})"/>`;
    body += `<ellipse cx="540" cy="820" rx="470" ry="560" fill="none" stroke="#fff" stroke-opacity="0.035" stroke-width="6"/>`;
  } else {
    defs.push(`<radialGradient id="${g}" cx="0.5" cy="0.4" r="0.9"><stop offset="0" stop-color="#5B3BC4"/><stop offset="0.55" stop-color="#2E1B6E"/><stop offset="1" stop-color="#140B33"/></radialGradient>`);
    body += `<rect width="${w}" height="${h}" fill="url(#${g})"/>`;
    const cx = 540, cy = 760, n = 28;
    for (let i = 0; i < n; i++) {
      const a0 = (i / n) * Math.PI * 2, a1 = a0 + Math.PI / n, R = 2200;
      body += `<path d="M${cx} ${cy} L${f(cx + R * Math.cos(a0))} ${f(cy + R * Math.sin(a0))} L${f(cx + R * Math.cos(a1))} ${f(cy + R * Math.sin(a1))} Z" fill="#fff" opacity="0.035"/>`;
    }
    const blur = id('f');
    defs.push(`<filter id="${blur}"><feGaussianBlur stdDeviation="60"/></filter>`);
    body += `<circle cx="540" cy="760" r="360" fill="#8E6CFF" opacity="0.35" filter="url(#${blur})"/>`;
    const dots = id('d');
    defs.push(`<pattern id="${dots}" width="60" height="60" patternUnits="userSpaceOnUse"><circle cx="30" cy="30" r="2.2" fill="#fff" opacity="0.08"/></pattern>`);
    body += `<rect width="${w}" height="${h}" fill="url(#${dots})"/>`;
  }
  return svg(w, h, body, defs.join(''));
}

// ---------- atlas building ----------

// Shelf packer: places sprites left-to-right in rows of the given width.
function pack(items, width, pad) {
  let x = pad, y = pad, rowH = 0;
  for (const it of items) {
    if (x + it.w + pad > width) { x = pad; y += rowH + pad; rowH = 0; }
    it.x = x; it.y = y;
    x += it.w + pad;
    rowH = Math.max(rowH, it.h);
  }
  return y + rowH + pad;
}

function sizeOf(svgText) {
  const m = /width="(\d+(?:\.\d+)?)" height="(\d+(?:\.\d+)?)"/.exec(svgText);
  return { w: Math.ceil(+m[1]), h: Math.ceil(+m[2]) };
}

function renderAtlas(name, items, width, pad = 4) {
  for (const it of items) Object.assign(it, sizeOf(it.svg));
  const height = Math.ceil(pack(items, width, pad) / 4) * 4;
  const html = `<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;padding:0;background:transparent;overflow:hidden}` +
    `div{position:absolute;line-height:0}</style></head><body>` +
    items.map(it => `<div style="left:${it.x}px;top:${it.y}px">${it.svg}</div>`).join('') + `</body></html>`;
  const htmlPath = path.join(WORK, name + '.html');
  fs.writeFileSync(htmlPath, html);
  const png = path.join(OUT, name + '.png');
  shoot(htmlPath, png, width, height);
  const map = {
    texture: name + '.png', width, height,
    sprites: items.map(it => ({ name: it.name, x: it.x, y: it.y, w: it.w, h: it.h, border: it.border || [0, 0, 0, 0] })),
  };
  fs.writeFileSync(path.join(OUT, name + '.json'), JSON.stringify(map, null, 1));
  console.log(`${name}: ${width}x${height}, ${items.length} sprites`);
}

function renderSingle(name, svgText) {
  const { w, h } = sizeOf(svgText);
  const htmlPath = path.join(WORK, name + '.html');
  fs.writeFileSync(htmlPath, `<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;padding:0;background:transparent;overflow:hidden}</style></head><body>${svgText}</body></html>`);
  shoot(htmlPath, path.join(OUT, name + '.png'), w, h);
  console.log(`${name}: ${w}x${h}`);
}

function shoot(htmlPath, pngPath, w, h) {
  const profile = path.join(WORK, 'edge-profile');
  if (fs.existsSync(pngPath)) fs.unlinkSync(pngPath);
  execFileSync(EDGE, [
    '--headless=new', '--disable-gpu', '--hide-scrollbars', '--force-device-scale-factor=1', '--no-first-run',
    '--default-background-color=00000000', `--user-data-dir=${profile}`, `--window-size=${w},${h}`,
    `--screenshot=${pngPath}`, 'file:///' + htmlPath.replace(/\\/g, '/'),
  ], { stdio: 'pipe', timeout: 120000 });
  if (!fs.existsSync(pngPath)) throw new Error('Edge did not write ' + pngPath);
}

function main() {
  fs.mkdirSync(OUT, { recursive: true });
  fs.mkdirSync(WORK, { recursive: true });
  const only = process.argv[2];

  if (!only || only === 'cards') {
    const cards = [];
    ORDER.forEach(c => { for (let v = 0; v <= 12; v++) cards.push({ name: `card_${c}_${v}`, svg: v <= 9 ? numberCard(c, v) : actionCard(c, v) }); });
    cards.push({ name: 'card_wild', svg: wildCard(false) });
    cards.push({ name: 'card_wild4', svg: wildCard(true) });
    cards.push({ name: 'card_back', svg: cardBack() });
    cards.push({ name: 'card_mask', svg: cardSilhouette('#fff', 0, 0, 1) });
    renderAtlas('cards', cards, 13 * 304 + 4, 4);
  }

  if (!only || only === 'ui') {
    const ui = [
      { name: 'card_shadow', svg: cardSilhouette('#000', 12, 34, 0.55, 0.98) },
      { name: 'card_glow', svg: cardSilhouette('#fff', 14, 34, 1, 1.04) },
      { name: 'rounded', svg: roundRect(192, 192, 64), border: [70, 70, 70, 70] },
      { name: 'rounded_small', svg: roundRect(64, 64, 18), border: [22, 22, 22, 22] },
      { name: 'panel', svg: panel(), border: [56, 56, 56, 56] },
      { name: 'btn_green', svg: glossyButton('#2FAE4E', '#8BE36A', '#1A7434'), border: [70, 70, 70, 60] },
      { name: 'btn_blue', svg: glossyButton('#1C7FE0', '#6CC0FF', '#0E4E9C'), border: [70, 70, 70, 60] },
      { name: 'btn_red', svg: glossyButton('#E5303C', '#FF7A6B', '#9C1521'), border: [70, 70, 70, 60] },
      { name: 'btn_yellow', svg: glossyButton('#F7B500', '#FFE36E', '#B07400'), border: [70, 70, 70, 60] },
      { name: 'btn_gray', svg: glossyButton('#56656B', '#8A9BA1', '#323D41'), border: [70, 70, 70, 60] },
      { name: 'circle', svg: circle(256) },
      { name: 'ring', svg: ring(256, 14) },
      { name: 'ring_thick', svg: ring(256, 30) },
      { name: 'glow', svg: softGlow(256) },
      { name: 'rays', svg: rays(512, 18) },
      { name: 'ball_red', svg: colorBall('red') },
      { name: 'ball_yellow', svg: colorBall('yellow') },
      { name: 'ball_green', svg: colorBall('green') },
      { name: 'ball_blue', svg: colorBall('blue') },
      { name: 'uno_burst', svg: burst(520, 320, 14, 1, 0.78, '#FF5A4E', '#C81E2B', 'UNO!', 150) },
      { name: 'vs', svg: vsBadge() },
      { name: 'ribbon_gold', svg: ribbon('#FFC21A', '#C78100') },
      { name: 'ribbon_gray', svg: ribbon('#7D8C92', '#4B575C') },
      { name: 'bubble', svg: bubble() },
      { name: 'fx_skip', svg: fxSkip() },
      { name: 'fx_reverse', svg: fxReverse() },
      { name: 'coin', svg: coin() },
      { name: 'crown', svg: crown() },
    ];
    for (const [k, v] of Object.entries(ICONS)) ui.push({ name: 'icon_' + k, svg: v });
    AVATARS.forEach((a, i) => ui.push({ name: 'avatar_' + i, svg: a }));
    EMOTES.forEach((e, i) => ui.push({ name: 'emote_' + i, svg: e }));
    renderAtlas('ui', ui, 2048, 4);
  }

  if (!only || only === 'big') {
    renderSingle('logo', logo());
    renderSingle('bg_table', background('table'));
    renderSingle('bg_home', background('home'));
  }

  if (!only || only === 'icons') {
    const iconDir = path.join(ROOT, 'Assets', 'UnoGame', 'Art', 'Icons');
    fs.mkdirSync(iconDir, { recursive: true });
    const save = (name, s) => { const tmp = path.join(OUT, name + '.png'); renderSingle(name, s); fs.renameSync(tmp, path.join(iconDir, name + '.png')); };
    save('icon_full', appIcon(1024, true, true));
    save('icon_bg', appIcon(432, true, false));
    save('icon_fg', appIcon(432, false, true));
  }
}

// App icon. Adaptive icons get a background layer and a foreground layer that keeps its content inside
// the central 66% safe zone; the legacy icon has both.
function appIcon(size, withBg, withFg) {
  const s = size / 432, defs = [];
  let body = '';
  if (withBg) {
    const g = id('g');
    defs.push(`<radialGradient id="${g}" cx="0.5" cy="0.4" r="0.8"><stop offset="0" stop-color="#6A47D8"/><stop offset="0.6" stop-color="#3A2290"/><stop offset="1" stop-color="#1A0F45"/></radialGradient>`);
    body += `<rect width="${size}" height="${size}" fill="url(#${g})"/>`;
    for (let i = 0; i < 20; i++) {
      const a0 = (i / 20) * Math.PI * 2, a1 = a0 + Math.PI / 20, R = size, c = size / 2;
      body += `<path d="M${c} ${c} L${f(c + R * Math.cos(a0))} ${f(c + R * Math.sin(a0))} L${f(c + R * Math.cos(a1))} ${f(c + R * Math.sin(a1))} Z" fill="#fff" opacity="0.05"/>`;
    }
  }
  if (withFg) {
    let fg = '';
    const fan = [['blue', -22], ['green', 0], ['red', 22]];
    fan.forEach(([c, rot], i) => {
      const cx = 216 + (i - 1) * 30, cy = 196;
      fg += `<g transform="rotate(${rot} ${cx} ${cy + 140})">` + rrect(cx - 62, cy - 88, 124, 174, 16, `fill="#fff" stroke="${INK}" stroke-width="5"`) +
        rrect(cx - 52, cy - 78, 104, 154, 10, `fill="${COL[c].base}"`) +
        `<path d="${leafPath(cx - 34, cy - 50, 68, 100, 10, 34, 10, 34)}" fill="#fff"/>` + `</g>`;
    });
    fg += `<g transform="rotate(-8 216 216)">` + text(220, 262, 112, 'UNO', INK, INK, 26, 'opacity="0.4"') +
      text(216, 254, 112, 'UNO', '#FFD84A', INK, 20) + text(216, 254, 112, 'UNO', '#FFD84A', '#fff', 5) + `</g>`;
    body += `<g transform="scale(${s})">${fg}</g>`;
  }
  return svg(size, size, body, defs.join(''));
}

main();
