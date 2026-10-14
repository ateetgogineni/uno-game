'use strict';
// End-to-end: real WebSocket clients against the server (Node's built-in WebSocket client).
const test = require('node:test');
const assert = require('node:assert');
const { createServer } = require('../src/index');
const { cardOf, DRAW2, WILD, WILD4 } = require('../src/engine');

const FAST = {
  botAfterMs: 300, botJitterMs: 100, turnMs: 3000, offlineTurnMs: 500, readyTimeoutMs: 3000,
  reconnectGraceMs: 800, botThinkMs: 10, botThinkJitterMs: 10, latencyGraceMs: 50,
  animScale: 0, // don't wait for client animations in tests
};

async function startServer(over = {}) {
  const srv = createServer(Object.assign({}, FAST, over), () => {});
  await new Promise(r => srv.server.listen(0, '127.0.0.1', r));
  srv.url = `ws://127.0.0.1:${srv.server.address().port}/`;
  return srv;
}

// A scripted client that plays legal moves from the state it is sent.
class Client {
  constructor(url, name, opts = {}) {
    this.name = name;
    this.msgs = [];
    this.waiters = [];
    this.st = null;
    this.events = [];
    this.autoplay = opts.autoplay !== false;
    this.ws = new WebSocket(url);
    this.ws.onmessage = e => this.onMessage(JSON.parse(e.data));
    this.opened = new Promise((res, rej) => { this.ws.onopen = res; this.ws.onerror = rej; });
  }
  send(o) { this.ws.send(JSON.stringify(o)); }
  onMessage(m) {
    this.msgs.push(m);
    if (m.t === 'up' || m.t === 'sync') {
      if (m.ev) this.events.push(...m.ev);
      this.st = m.st;
      if (this.autoplay) setTimeout(() => this.act(), 5);
    }
    this.waiters = this.waiters.filter(w => { if (w.pred(m)) { w.resolve(m); return false; } return true; });
  }
  waitFor(pred, ms = 8000) {
    const found = this.msgs.find(pred);
    if (found) return Promise.resolve(found);
    return new Promise((resolve, reject) => {
      const w = { pred, resolve };
      this.waiters.push(w);
      setTimeout(() => reject(new Error(`${this.name}: timed out waiting`)), ms).unref();
    });
  }
  legal() {
    const st = this.st;
    if (!st || st.over || st.turn !== st.you) return [];
    const top = cardOf(st.top);
    const ok = cid => {
      const c = cardOf(cid);
      if (st.pending > 0) return c.v === DRAW2 || c.v === WILD4;
      if (c.v === WILD) return true;
      if (c.v === WILD4) return !st.hand.some(h => h !== cid && cardOf(h).c === st.color);
      return c.c === st.color || c.v === top.v;
    };
    if (st.hasDrawn) return st.drawn >= 0 && ok(st.drawn) ? [st.drawn] : [];
    return st.hand.filter(ok);
  }
  act() {
    const st = this.st;
    if (!st || st.over || st.turn !== st.you || this.ws.readyState !== 1) return;
    const legal = this.legal();
    if (legal.length) {
      const card = legal[0];
      this.send({ t: 'play', card, color: cardOf(card).c === 4 ? 2 : -1, uno: st.hand.length === 2 });
    } else this.send({ t: st.hasDrawn ? 'pass' : 'draw' });
  }
}

test('no opponent: a bot joins and the game is played to the end', async () => {
  const srv = await startServer();
  try {
    const a = new Client(srv.url, 'Alice');
    await a.opened;
    a.send({ t: 'hello', name: 'Alice', avatar: 3, pid: 'pid-a' });
    const welcome = await a.waitFor(m => m.t === 'welcome');
    assert.strictEqual(welcome.online, 1);
    a.send({ t: 'find' });
    const matched = await a.waitFor(m => m.t === 'matched');
    assert.strictEqual(matched.players.length, 2);
    assert.strictEqual(matched.players[matched.seat].name, 'Alice');
    assert.notStrictEqual(matched.players[1 - matched.seat].avatar, 3);
    a.send({ t: 'ready' });
    const first = await a.waitFor(m => m.t === 'up');
    const deal = first.ev.filter(e => e.k === 'deal');
    assert.strictEqual(deal.length, 2);
    assert.strictEqual(deal.find(e => e.p === matched.seat).cards.length, 7);
    assert.strictEqual(deal.find(e => e.p !== matched.seat).cards.length, 0, 'opponent cards are hidden');
    const over = await a.waitFor(m => m.t === 'up' && m.st.over === 1, 60000);
    assert.ok(over.st.winner === 0 || over.st.winner === 1);
    assert.ok(a.events.some(e => e.k === 'win'));
    a.ws.close();
  } finally { await srv.shutdown(); }
});

test('two players searching are matched with each other and finish a game', async () => {
  const srv = await startServer({ botAfterMs: 5000 });
  try {
    const a = new Client(srv.url, 'Alice');
    const b = new Client(srv.url, 'Bob');
    await Promise.all([a.opened, b.opened]);
    a.send({ t: 'hello', name: 'Alice', avatar: 1, pid: 'pid-a' });
    b.send({ t: 'hello', name: 'Bob', avatar: 2, pid: 'pid-b' });
    await Promise.all([a.waitFor(m => m.t === 'welcome'), b.waitFor(m => m.t === 'welcome')]);
    a.send({ t: 'find' });
    await a.waitFor(m => m.t === 'queued');
    b.send({ t: 'find' });
    const [ma, mb] = await Promise.all([a.waitFor(m => m.t === 'matched'), b.waitFor(m => m.t === 'matched')]);
    assert.strictEqual(ma.match, mb.match);
    assert.notStrictEqual(ma.seat, mb.seat);
    assert.strictEqual(ma.players[mb.seat].name, 'Bob');
    a.send({ t: 'ready' });
    b.send({ t: 'ready' });
    const [oa, ob] = await Promise.all([
      a.waitFor(m => m.t === 'up' && m.st.over === 1, 60000),
      b.waitFor(m => m.t === 'up' && m.st.over === 1, 60000),
    ]);
    assert.strictEqual(oa.st.winner, ob.st.winner);
    // Both saw the same public sequence of plays.
    const plays = c => c.events.filter(e => e.k === 'play').map(e => e.card).join(',');
    assert.strictEqual(plays(a), plays(b));
    a.ws.close(); b.ws.close();
  } finally { await srv.shutdown(); }
});

test('a player who disconnects and does not return forfeits', async () => {
  const srv = await startServer({ botAfterMs: 5000, turnMs: 20000 });
  try {
    const a = new Client(srv.url, 'Alice', { autoplay: false });
    const b = new Client(srv.url, 'Bob', { autoplay: false });
    await Promise.all([a.opened, b.opened]);
    a.send({ t: 'hello', name: 'Alice', pid: 'pid-a' });
    b.send({ t: 'hello', name: 'Bob', pid: 'pid-b' });
    a.send({ t: 'find' });
    await a.waitFor(m => m.t === 'queued');
    b.send({ t: 'find' });
    const mb = await b.waitFor(m => m.t === 'matched');
    a.send({ t: 'ready' });
    b.send({ t: 'ready' });
    await b.waitFor(m => m.t === 'up');
    a.ws.close();
    const over = await b.waitFor(m => m.t === 'up' && m.st.over === 1, 5000);
    assert.strictEqual(over.st.winner, mb.seat);
    assert.ok(over.ev.some(e => e.k === 'win' && e.reason === 'left'));
    b.ws.close();
  } finally { await srv.shutdown(); }
});

test('a player who reconnects in time gets the game back', async () => {
  const srv = await startServer({ botAfterMs: 5000, turnMs: 20000, reconnectGraceMs: 3000 });
  try {
    const a = new Client(srv.url, 'Alice', { autoplay: false });
    const b = new Client(srv.url, 'Bob', { autoplay: false });
    await Promise.all([a.opened, b.opened]);
    a.send({ t: 'hello', name: 'Alice', pid: 'pid-a' });
    b.send({ t: 'hello', name: 'Bob', pid: 'pid-b' });
    a.send({ t: 'find' });
    await a.waitFor(m => m.t === 'queued');
    b.send({ t: 'find' });
    const ma = await a.waitFor(m => m.t === 'matched');
    a.send({ t: 'ready' });
    b.send({ t: 'ready' });
    const up = await a.waitFor(m => m.t === 'up');
    const hand = up.st.hand.slice().sort().join(',');
    a.ws.close();
    await new Promise(r => setTimeout(r, 300));
    const a2 = new Client(srv.url, 'Alice2', { autoplay: false });
    await a2.opened;
    a2.send({ t: 'hello', name: 'Alice', pid: 'pid-a', resume: ma.match });
    const welcome = await a2.waitFor(m => m.t === 'welcome');
    assert.strictEqual(welcome.resumed, 1);
    const sync = await a2.waitFor(m => m.t === 'sync');
    assert.strictEqual(sync.seat, ma.seat);
    assert.strictEqual(sync.st.hand.slice().sort().join(','), hand);
    a2.ws.close(); b.ws.close();
  } finally { await srv.shutdown(); }
});

test('health endpoint and malformed input', async () => {
  const srv = await startServer();
  try {
    const port = srv.server.address().port;
    const res = await fetch(`http://127.0.0.1:${port}/health`);
    const body = await res.json();
    assert.strictEqual(body.ok, true);
    const a = new Client(srv.url, 'Mallory', { autoplay: false });
    await a.opened;
    a.ws.send('not json');
    a.send({ t: 'play', card: 5 });         // not in a match
    a.send({ t: 'hello', name: '<script>alert(1)</script>', avatar: 999 });
    const w = await a.waitFor(m => m.t === 'welcome');
    assert.strictEqual(w.v, 1);
    const s = [...srv.lobby.sessions][0];
    assert.strictEqual(s.name, 'scriptalert1sc'); // stripped to safe characters, max 14
    assert.strictEqual(s.avatar, 11);
    a.ws.close();
  } finally { await srv.shutdown(); }
});
