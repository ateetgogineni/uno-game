'use strict';
// Minimal RFC 6455 WebSocket server (text frames, ping/pong, close, fragmentation). No dependencies.
const crypto = require('crypto');
const { EventEmitter } = require('events');

const GUID = '258EAFA5-E914-47DA-95CA-C5AB0DC85B11';
const MAX_PAYLOAD = 64 * 1024;

class WsConnection extends EventEmitter {
  constructor(socket, req) {
    super();
    this.socket = socket;
    this.ip = (req.headers['x-forwarded-for'] || req.socket.remoteAddress || '').toString().split(',')[0].trim();
    this.buf = Buffer.alloc(0);
    this.fragments = null;
    this.fragOpcode = 0;
    this.fragSize = 0;
    this.open = true;
    this.lastSeen = Date.now();
    socket.setNoDelay(true);
    socket.on('data', d => this._onData(d));
    socket.on('close', () => this._closed());
    socket.on('end', () => this._closed());
    socket.on('error', () => this._closed());
  }

  _onData(d) {
    this.lastSeen = Date.now();
    this.buf = this.buf.length ? Buffer.concat([this.buf, d]) : d;
    while (this.open && this.buf.length >= 2) {
      const b0 = this.buf[0], b1 = this.buf[1];
      if (b0 & 0x70) return this.close(1002);
      const fin = (b0 & 0x80) !== 0;
      const opcode = b0 & 0x0f;
      const masked = (b1 & 0x80) !== 0;
      let len = b1 & 0x7f, off = 2;
      if (len === 126) {
        if (this.buf.length < 4) return;
        len = this.buf.readUInt16BE(2); off = 4;
      } else if (len === 127) {
        if (this.buf.length < 10) return;
        if (this.buf.readUInt32BE(2) !== 0) return this.close(1009);
        len = this.buf.readUInt32BE(6); off = 10;
      }
      if (len > MAX_PAYLOAD) return this.close(1009);
      if (!masked) return this.close(1002); // browsers/clients must mask
      if (this.buf.length < off + 4 + len) return;
      const mask = this.buf.subarray(off, off + 4);
      off += 4;
      const payload = Buffer.from(this.buf.subarray(off, off + len));
      for (let i = 0; i < len; i++) payload[i] ^= mask[i & 3];
      this.buf = this.buf.subarray(off + len);
      this._frame(fin, opcode, payload);
    }
  }

  _frame(fin, opcode, payload) {
    if (opcode >= 8) {
      if (!fin || payload.length > 125) return this.close(1002);
      if (opcode === 8) { this.close(1000); return; }
      if (opcode === 9) { this._send(10, payload); return; }
      if (opcode === 10) return;
      return this.close(1002);
    }
    if (opcode === 0) {
      if (!this.fragments) return this.close(1002);
      this.fragments.push(payload);
      this.fragSize += payload.length;
      if (this.fragSize > MAX_PAYLOAD) return this.close(1009);
      if (fin) {
        const data = Buffer.concat(this.fragments);
        const op = this.fragOpcode;
        this.fragments = null;
        this._message(op, data);
      }
      return;
    }
    if (this.fragments) return this.close(1002);
    if (!fin) {
      this.fragments = [payload];
      this.fragOpcode = opcode;
      this.fragSize = payload.length;
      return;
    }
    this._message(opcode, payload);
  }

  _message(opcode, data) {
    if (opcode === 1) this.emit('message', data.toString('utf8'));
  }

  send(text) {
    if (this.open) this._send(1, Buffer.from(text, 'utf8'));
  }

  ping() {
    if (this.open) this._send(9, Buffer.alloc(0));
  }

  _send(opcode, payload) {
    if (this.socket.destroyed) return;
    const len = payload.length;
    let header;
    if (len < 126) {
      header = Buffer.from([0x80 | opcode, len]);
    } else if (len < 65536) {
      header = Buffer.alloc(4);
      header[0] = 0x80 | opcode; header[1] = 126; header.writeUInt16BE(len, 2);
    } else {
      header = Buffer.alloc(10);
      header[0] = 0x80 | opcode; header[1] = 127; header.writeUInt32BE(0, 2); header.writeUInt32BE(len, 6);
    }
    this.socket.write(Buffer.concat([header, payload]));
  }

  close(code = 1000) {
    if (!this.open) return;
    const p = Buffer.alloc(2);
    p.writeUInt16BE(code, 0);
    this._send(8, p);
    this.open = false;
    this.socket.end();
    setTimeout(() => this.socket.destroy(), 1000).unref();
    this.emit('close');
  }

  terminate() {
    this._closed();
  }

  _closed() {
    if (!this.open) { this.socket.destroy(); return; }
    this.open = false;
    this.socket.destroy();
    this.emit('close');
  }
}

function attachWebSocket(server, onConnection) {
  server.on('upgrade', (req, socket, head) => {
    const key = req.headers['sec-websocket-key'];
    const upgrade = (req.headers.upgrade || '').toLowerCase();
    if (upgrade !== 'websocket' || !key || req.headers['sec-websocket-version'] !== '13') {
      socket.end('HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n');
      return;
    }
    const accept = crypto.createHash('sha1').update(key + GUID).digest('base64');
    socket.write('HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n' +
      'Sec-WebSocket-Accept: ' + accept + '\r\n\r\n');
    const conn = new WsConnection(socket, req);
    onConnection(conn);
    if (head && head.length) conn._onData(head);
  });
}

module.exports = { attachWebSocket, WsConnection };
