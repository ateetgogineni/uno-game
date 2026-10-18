'use strict';
const crypto = require('crypto');
const { Game } = require('./engine');
const { chooseMove, applyMove, clampLevel, CATCH_CHANCE } = require('./bot');

// Emote ids shared with the client (EmoteId in C#).
const EMOTE = { HAPPY: 0, LAUGH: 1, COOL: 2, WOW: 3, ANGRY: 4, SAD: 5 };

// One game of 2 to 10 players. Seats are humans (with a lobby session) or bots. The server owns the game
// state; clients only send intents (play/draw/pass) and animate the events they get back.
class Match {
  constructor(lobby, seats, cfg, mode = 'classic') {
    this.mode = mode;
    this.lobby = lobby;
    this.cfg = cfg;
    this.id = crypto.randomBytes(6).toString('hex');
    this.seats = seats.map(s => Object.assign({ connected: !s.bot, ready: false, timeouts: 0, graceTimer: null, lastEmote: 0 }, s));
    this.game = null;
    this.state = 'waiting'; // waiting -> playing -> over
    this.turnTimer = null;
    this.botTimer = null;
    this.readyTimer = null;
    this.deadline = 0;
    this.turnLen = cfg.turnMs;
  }

  playersInfo() {
    return this.seats.map(s => ({ name: s.name, avatar: s.avatar }));
  }

  seatForPid(pid) {
    return this.seats.findIndex(s => !s.bot && s.pid === pid);
  }

  sendTo(seat, obj) {
    const s = this.seats[seat];
    if (s && s.session && s.connected) this.lobby.send(s.session, obj);
  }

  begin() {
    this.seats.forEach((s, i) => {
      if (!s.session) return;
      s.session.match = this;
      s.session.seat = i;
      this.sendTo(i, { t: 'matched', match: this.id, seat: i, mode: this.mode, players: this.playersInfo() });
    });
    this.readyTimer = setTimeout(() => this.startGame(), this.cfg.readyTimeoutMs);
  }

  ready(seat) {
    const s = this.seats[seat];
    if (!s) return;
    s.ready = true;
    if (this.state === 'waiting' && this.seats.every(x => x.bot || x.ready)) this.startGame();
  }

  startGame() {
    if (this.state !== 'waiting') return;
    clearTimeout(this.readyTimer);
    this.state = 'playing';
    this.game = new Game({ players: this.seats.length, seed: crypto.randomInt(1, 2 ** 31 - 1), stacking: this.cfg.stacking, mode: this.mode });
    this.game.start();
    this.lobby.log(`match ${this.id} started: ${this.seats.map(s => s.name + (s.bot ? ' (bot)' : '')).join(' vs ')}`);
    this.flush();
  }

  stateFor(seat) {
    return Object.assign(this.game.view(seat), { ms: Math.max(0, this.deadline - Date.now()), turnMs: this.turnLen });
  }

  flush() {
    const events = this.game.takeEvents();
    this.armTimers(events);
    for (let i = 0; i < this.seats.length; i++) {
      if (this.seats[i].bot) continue;
      this.sendTo(i, { t: 'up', ev: this.game.eventsFor(i, events), st: this.stateFor(i) });
    }
    this.botReactions(events);
    if (this.game.over) this.end();
    else if (!this.seats.some((s, i) => !s.bot && s.session && !this.game.out[i])) this.abandon('no players left');
  }

  sendSync(seat) {
    if (!this.game) return;
    this.sendTo(seat, { t: 'sync', match: this.id, seat, players: this.playersInfo(), st: this.stateFor(seat) });
  }

  // Rough time the clients need to animate these events, so bots don't act before the cards land.
  animMs(events) {
    // dealing: one card each per round, a little faster per card at big tables (as the clients deal)
    const n = this.seats.length;
    const deal = Math.min(1, (2 + 0.24 * (n - 2)) / n);
    let ms = 0;
    for (const ev of events) {
      if (ev.k === 'deal') ms += 160 * ev.n * deal;
      else if (ev.k === 'draw' || ev.k === 'unoPen') ms += 220 * Math.max(1, ev.n);
      else if (ev.k === 'play') ms += 450;
      else if (ev.k === 'reshuffle') ms += 600;
      else if (ev.k === 'launch') ms += 500 + 200 * ev.n;
      else if (ev.k === 'roulette') ms += 400 + 200 * ev.n;
      else if (ev.k === 'discardAll') ms += 250 * ev.n;
      else if (ev.k === 'flip') ms += 1000;
      else if (ev.k === 'swap' || ev.k === 'rotate') ms += 900;
      else if (ev.k === 'out') ms += 700;
      else if (ev.k === 'dare' || ev.k === 'darePending' || ev.k === 'roulettePending') ms += 400;
    }
    return ms * (this.cfg.animScale === undefined ? 1 : this.cfg.animScale);
  }

  armTimers(events) {
    clearTimeout(this.turnTimer);
    clearTimeout(this.botTimer);
    if (this.game.over) { this.deadline = 0; return; }
    const seat = this.seats[this.game.turn];
    const anim = this.animMs(events);
    const len = seat.bot || seat.connected ? this.cfg.turnMs : this.cfg.offlineTurnMs;
    this.turnLen = len;
    this.deadline = Date.now() + anim + len;
    this.turnTimer = setTimeout(() => this.onTimeout(), anim + len + this.cfg.latencyGraceMs);
    if (seat.bot) {
      // Big tables move a bit quicker so the wait for your next turn stays short.
      const pace = this.seats.length > 4 ? 0.75 : 1;
      const think = Math.round((this.cfg.botThinkMs + Math.floor(Math.random() * this.cfg.botThinkJitterMs)) * pace);
      this.botTimer = setTimeout(() => this.botAct(), anim + think);
    }
  }

  onTimeout() {
    if (this.state !== 'playing') return;
    const p = this.game.turn;
    const s = this.seats[p];
    if (s.bot) { this.botAct(); return; }
    s.timeouts++;
    if (s.timeouts >= this.cfg.maxTimeouts) {
      this.lobby.log(`match ${this.id}: ${s.name} idle, forfeits`);
      this.game.forfeit(p, 'idle');
    } else {
      this.game.autoMove(p);
    }
    this.flush();
  }

  botAct() {
    if (this.state !== 'playing') return;
    const p = this.game.turn;
    if (!this.seats[p].bot) return;
    const err = applyMove(this.game, p, chooseMove(this.game, p, this.seats[p].level));
    if (err) this.game.autoMove(p);
    this.flush();
  }

  action(seat, msg) {
    if (this.state !== 'playing') return;
    let err;
    if (msg.t === 'play') {
      const color = Number.isInteger(msg.color) ? msg.color : -1;
      const target = Number.isInteger(msg.target) ? msg.target : -1;
      // A forgotten UNO goes unpunished only if nobody notices: every opponent is a bot and each one misses it.
      const others = this.seats.filter((s, i) => i !== seat && !this.game.out[i]);
      const forgive = !msg.uno && others.every(s => s.bot) && others.every(s => Math.random() >= CATCH_CHANCE[clampLevel(s.level)]);
      err = this.game.play(seat, msg.card | 0, color, !!msg.uno, forgive, target);
    } else if (msg.t === 'draw') err = this.game.draw(seat);
    else if (msg.t === 'pass') err = this.game.pass(seat);
    else if (msg.t === 'roulette') err = this.game.roulette(seat, Number.isInteger(msg.color) ? msg.color : -1);
    else if (msg.t === 'dare') err = this.game.dare(seat, !!msg.ok);
    else return;
    if (err) {
      this.sendTo(seat, { t: 'err', msg: err });
      this.sendSync(seat);
      return;
    }
    this.seats[seat].timeouts = 0;
    this.flush();
  }

  emote(seat, e) {
    const s = this.seats[seat];
    const now = Date.now();
    if (!s || now - s.lastEmote < 1500 || !(e >= 0 && e <= 5)) return;
    s.lastEmote = now;
    for (let i = 0; i < this.seats.length; i++) if (i !== seat) this.sendTo(i, { t: 'emote', p: seat, e });
  }

  // Bots occasionally react with an emote, like a person would.
  botReactions(events) {
    for (let i = 0; i < this.seats.length; i++) {
      if (!this.seats[i].bot) continue;
      let e = -1;
      for (const ev of events) {
        if (ev.k === 'win') e = ev.p === i ? (Math.random() < 0.5 ? EMOTE.HAPPY : EMOTE.COOL) : EMOTE.SAD;
        else if (ev.k === 'stack' && this.game.turn !== i) e = EMOTE.LAUGH;
        else if (ev.k === 'draw' && ev.p === i && ev.n >= 4) e = Math.random() < 0.5 ? EMOTE.ANGRY : EMOTE.WOW;
      }
      if (e < 0 || Math.random() > 0.35) continue;
      setTimeout(() => {
        for (let j = 0; j < this.seats.length; j++) if (j !== i) this.sendTo(j, { t: 'emote', p: i, e });
      }, 700 + Math.floor(Math.random() * 600));
    }
  }

  leave(seat) {
    const s = this.seats[seat];
    if (!s) return;
    if (this.state === 'waiting') { this.replaceWithBot(seat); return; }
    if (this.state === 'playing') {
      this.detach(seat);
      if (this.game.out[seat]) return; // already out of this game, just watching
      this.lobby.log(`match ${this.id}: ${s.name} left`);
      this.game.forfeit(seat, 'left');
      this.flush();
    }
  }

  detach(seat) {
    const s = this.seats[seat];
    if (s.session) { s.session.match = null; s.session.seat = -1; }
    s.session = null;
    s.connected = false;
  }

  replaceWithBot(seat) {
    const s = this.seats[seat];
    this.detach(seat);
    const human = this.seats.find((x, i) => i !== seat && x.session);
    const bot = this.lobby.makeBot(this.seats.filter((x, i) => i !== seat).map(x => x.avatar), human ? human.session.botLevel : undefined);
    this.seats[seat] = Object.assign(s, bot, { connected: false, ready: true });
    // Tell the other players who they are playing now.
    this.seats.forEach((x, i) => { if (i !== seat && x.session) this.sendTo(i, { t: 'matched', match: this.id, seat: i, mode: this.mode, players: this.playersInfo() }); });
    if (this.state === 'waiting' && this.seats.every(x => x.bot || x.ready)) this.startGame();
  }

  disconnected(seat, session) {
    const s = this.seats[seat];
    if (!s || s.session !== session) return;
    s.connected = false;
    if (this.state === 'waiting') { this.replaceWithBot(seat); return; }
    if (this.state !== 'playing') return;
    clearTimeout(s.graceTimer);
    s.graceTimer = setTimeout(() => {
      if (this.state !== 'playing' || s.connected) return;
      this.lobby.log(`match ${this.id}: ${s.name} did not come back, forfeits`);
      this.detach(seat);
      if (this.game.out[seat]) return;
      this.game.forfeit(seat, 'left');
      this.flush();
    }, this.cfg.reconnectGraceMs);
  }

  reconnect(seat, session) {
    const s = this.seats[seat];
    if (s.session && s.session !== session) { s.session.match = null; s.session.ws.terminate(); }
    clearTimeout(s.graceTimer);
    s.session = session;
    s.connected = true;
    session.match = this;
    session.seat = seat;
    this.lobby.log(`match ${this.id}: ${s.name} reconnected`);
    this.sendSync(seat);
  }

  end() {
    if (this.state === 'over') return;
    this.state = 'over';
    clearTimeout(this.turnTimer);
    clearTimeout(this.botTimer);
    clearTimeout(this.readyTimer);
    for (const s of this.seats) {
      clearTimeout(s.graceTimer);
      if (s.session) { s.session.match = null; s.session.seat = -1; }
    }
    const w = this.seats[this.game.winner];
    this.lobby.log(`match ${this.id} over: ${w ? w.name : '?'} wins (${this.game.points} pts)`);
    this.lobby.matchEnded(this);
  }

  // Only bots are left (every person left or was knocked out): stop without a winner.
  abandon(why) {
    if (this.state === 'over') return;
    this.state = 'over';
    clearTimeout(this.turnTimer);
    clearTimeout(this.botTimer);
    clearTimeout(this.readyTimer);
    for (const s of this.seats) {
      clearTimeout(s.graceTimer);
      if (s.session) { s.session.match = null; s.session.seat = -1; }
    }
    this.lobby.log(`match ${this.id} stopped: ${why}`);
    this.lobby.matchEnded(this);
  }

  dispose() {
    clearTimeout(this.turnTimer);
    clearTimeout(this.botTimer);
    clearTimeout(this.readyTimer);
    for (const s of this.seats) clearTimeout(s.graceTimer);
  }
}

module.exports = { Match, EMOTE };
