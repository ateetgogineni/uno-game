'use strict';
const { Match } = require('./match');

const PROTOCOL = 1;
const AVATAR_COUNT = 12;
const BOT_NAMES = [
  'Maya_21', 'Rohan77', 'LunaPlays', 'Arjun.K', 'Zoe99', 'Kabir_xo', 'NinjaCat', 'Priya.S', 'LeoWins',
  'Aisha23', 'Dev_Rocks', 'Sana.M', 'Max_Ace', 'Ishaan07', 'Nora_UNO', 'Vikram_V', 'Ella.B', 'Kian88',
  'Riya_Star', 'Omar.Q', 'Tara_Lee', 'Aryan_9', 'Mila_Moon', 'Neil.P', 'Jade_Fox', 'Sid_Smash',
];

function cleanName(v) {
  const s = String(v || '').replace(/[^A-Za-z0-9_. -]/g, '').trim().slice(0, 14);
  return s.length >= 2 ? s : 'Player';
}

function clampInt(v, lo, hi) {
  const n = Number.isInteger(v) ? v : lo;
  return Math.min(hi, Math.max(lo, n));
}

class Lobby {
  constructor(cfg, logger) {
    this.cfg = cfg;
    this.logger = logger || (() => {});
    this.sessions = new Set();
    this.queue = [];
    this.matches = new Map();
    this.nextId = 1;
    this.totalMatches = 0;
  }

  log(msg) { this.logger(msg); }

  send(s, obj) {
    if (s.ws.open) s.ws.send(JSON.stringify(obj));
  }

  connect(ws) {
    const s = { id: this.nextId++, ws, name: 'Player', avatar: 0, pid: '', match: null, seat: -1, botTimer: null, window: Date.now(), count: 0 };
    this.sessions.add(s);
    ws.on('message', text => this.onMessage(s, text));
    ws.on('close', () => this.onClose(s));
    return s;
  }

  onMessage(s, text) {
    // Flood guard: at most 40 messages per 5 seconds.
    const now = Date.now();
    if (now - s.window > 5000) { s.window = now; s.count = 0; }
    if (++s.count > 40) { s.ws.close(1008); return; }

    let msg;
    try { msg = JSON.parse(text); } catch (e) { return; }
    if (!msg || typeof msg.t !== 'string') return;
    const m = s.match;
    switch (msg.t) {
      case 'hello': this.hello(s, msg); break;
      case 'find': this.find(s); break;
      case 'cancel': this.dequeue(s); break;
      case 'ready': if (m) m.ready(s.seat); break;
      case 'sync': if (m) m.sendSync(s.seat); break;
      case 'play': case 'draw': case 'pass': if (m) m.action(s.seat, msg); break;
      case 'emote': if (m) m.emote(s.seat, msg.e | 0); break;
      case 'leave': if (m) m.leave(s.seat); break;
      case 'ping': this.send(s, { t: 'pong', online: this.sessions.size }); break;
    }
  }

  hello(s, msg) {
    s.name = cleanName(msg.name);
    s.avatar = clampInt(msg.avatar, 0, AVATAR_COUNT - 1);
    s.pid = String(msg.pid || '').slice(0, 64);
    let resumed = 0;
    if (s.pid && msg.resume) {
      const m = this.matches.get(String(msg.resume));
      const seat = m ? m.seatForPid(s.pid) : -1;
      if (m && seat >= 0 && m.state === 'playing') { m.reconnect(seat, s); resumed = 1; }
    }
    this.send(s, { t: 'welcome', v: PROTOCOL, online: this.sessions.size, resumed });
  }

  find(s) {
    if (s.match && s.match.state !== 'over') return;
    s.match = null;
    if (this.queue.includes(s)) return;
    const other = this.queue.find(o => o !== s && o.ws.open && (!s.pid || o.pid !== s.pid));
    if (other) {
      this.dequeue(other);
      this.createMatch([this.humanSeat(other), this.humanSeat(s)]);
      return;
    }
    this.queue.push(s);
    this.send(s, { t: 'queued', online: this.sessions.size });
    const wait = this.cfg.botAfterMs + Math.floor(Math.random() * this.cfg.botJitterMs);
    s.botTimer = setTimeout(() => {
      if (!this.queue.includes(s)) return;
      this.dequeue(s);
      this.createMatch([this.humanSeat(s), this.makeBot([s.avatar])]);
    }, wait);
  }

  dequeue(s) {
    clearTimeout(s.botTimer);
    s.botTimer = null;
    const i = this.queue.indexOf(s);
    if (i >= 0) this.queue.splice(i, 1);
  }

  humanSeat(s) {
    return { name: s.name, avatar: s.avatar, pid: s.pid, bot: false, session: s };
  }

  makeBot(takenAvatars) {
    let avatar;
    do { avatar = Math.floor(Math.random() * AVATAR_COUNT); } while (takenAvatars.includes(avatar));
    const name = BOT_NAMES[Math.floor(Math.random() * BOT_NAMES.length)];
    return { name, avatar, pid: '', bot: true, session: null };
  }

  createMatch(seats) {
    const m = new Match(this, seats, this.cfg);
    this.matches.set(m.id, m);
    this.totalMatches++;
    m.begin();
    return m;
  }

  matchEnded(m) {
    // Keep it briefly so late messages find it, then drop it.
    setTimeout(() => { m.dispose(); this.matches.delete(m.id); }, 5000).unref();
  }

  onClose(s) {
    this.sessions.delete(s);
    this.dequeue(s);
    if (s.match) s.match.disconnected(s.seat, s);
  }

  heartbeat() {
    const now = Date.now();
    for (const s of this.sessions) {
      if (now - s.ws.lastSeen > this.cfg.idleKickMs) s.ws.terminate();
      else s.ws.ping();
    }
  }

  stats() {
    let playing = 0;
    for (const m of this.matches.values()) if (m.state !== 'over') playing++;
    return { ok: true, protocol: PROTOCOL, online: this.sessions.size, queued: this.queue.length, matches: playing, totalMatches: this.totalMatches };
  }
}

module.exports = { Lobby, cleanName, AVATAR_COUNT, PROTOCOL };
