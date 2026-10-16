'use strict';
const http = require('http');
const { attachWebSocket } = require('./ws');
const { Lobby } = require('./lobby');

function num(name, def) {
  const v = parseInt(process.env[name], 10);
  return Number.isFinite(v) ? v : def;
}

const defaults = {
  port: num('PORT', 8090),
  botAfterMs: num('BOT_AFTER_MS', 6000),        // wait this long for a real opponent before a bot joins
  botJitterMs: num('BOT_JITTER_MS', 2500),
  turnMs: num('TURN_MS', 15000),
  offlineTurnMs: num('OFFLINE_TURN_MS', 5000),  // turn length while a player is disconnected
  latencyGraceMs: 600,
  readyTimeoutMs: num('READY_TIMEOUT_MS', 15000),
  reconnectGraceMs: num('RECONNECT_GRACE_MS', 30000),
  maxTimeouts: num('MAX_TIMEOUTS', 3),          // consecutive missed turns before a player forfeits
  botThinkMs: num('BOT_THINK_MS', 900),
  botThinkJitterMs: num('BOT_THINK_JITTER_MS', 900),
  idleKickMs: 45000,
  floodMax: 40,                                  // messages a client may send per 5 seconds
  stacking: process.env.STACKING !== '0',       // +2/+4 stacking, as in the reference game
};

function createServer(overrides = {}, logger = msg => console.log(new Date().toISOString(), msg)) {
  const cfg = Object.assign({}, defaults, overrides);
  const lobby = new Lobby(cfg, logger);
  const server = http.createServer((req, res) => {
    if (req.url === '/health') {
      res.writeHead(200, { 'content-type': 'application/json', 'cache-control': 'no-store' });
      res.end(JSON.stringify(lobby.stats()));
      return;
    }
    res.writeHead(200, { 'content-type': 'text/plain' });
    res.end('UNO Game server is running. Connect with a WebSocket.\n');
  });
  attachWebSocket(server, ws => lobby.connect(ws));
  const beat = setInterval(() => lobby.heartbeat(), 15000);
  server.on('close', () => clearInterval(beat));
  const shutdown = () => new Promise(resolve => {
    for (const s of [...lobby.sessions]) s.ws.terminate();
    for (const m of lobby.matches.values()) m.dispose();
    server.close(() => resolve());
  });
  return { server, lobby, cfg, shutdown };
}

if (require.main === module) {
  const { server, cfg } = createServer();
  const host = process.env.HOST || '0.0.0.0';
  server.listen(cfg.port, host, () => console.log(`UNO Game server listening on ${host}:${cfg.port} (bot after ${cfg.botAfterMs} ms, stacking ${cfg.stacking})`));
  const stop = () => { console.log('shutting down'); server.close(() => process.exit(0)); setTimeout(() => process.exit(0), 3000).unref(); };
  process.on('SIGTERM', stop);
  process.on('SIGINT', stop);
}

module.exports = { createServer, defaults };
