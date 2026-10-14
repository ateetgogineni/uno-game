using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UnoGame.Net
{
    /// <summary>
    /// WebSocket connection to the game server. Started from the main thread, so every await continues
    /// on Unity's main thread and the Message/Closed events are safe to handle directly.
    /// </summary>
    public sealed class NetClient : IDisposable
    {
        ClientWebSocket ws;
        CancellationTokenSource cts;
        readonly Queue<string> outbox = new Queue<string>();
        bool sending;
        int generation;

        public event Action<string> Message;
        public event Action Closed;

        public bool IsOpen => ws != null && ws.State == WebSocketState.Open;

        public async Task<bool> ConnectAsync(string url, int timeoutMs)
        {
            Shutdown();
            int gen = ++generation;
            var socket = new ClientWebSocket();
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            var source = new CancellationTokenSource();
            ws = socket;
            cts = source;
            try
            {
                Task connect = socket.ConnectAsync(new Uri(url), source.Token);
                Task first = await Task.WhenAny(connect, Task.Delay(timeoutMs));
                if (first != connect || connect.IsFaulted || connect.IsCanceled || socket.State != WebSocketState.Open)
                {
                    if (connect.IsFaulted) Debug.Log("[Net] connect failed: " + connect.Exception?.GetBaseException().Message);
                    else Debug.Log("[Net] connect timed out: " + url);
                    if (gen == generation) Shutdown();
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.Log("[Net] connect error: " + e.Message);
                if (gen == generation) Shutdown();
                return false;
            }
            if (gen != generation) return false;
            _ = ReceiveLoop(socket, source.Token, gen);
            return true;
        }

        async Task ReceiveLoop(ClientWebSocket socket, CancellationToken token, int gen)
        {
            var buffer = new byte[16 * 1024];
            try
            {
                while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    using (var ms = new MemoryStream())
                    {
                        WebSocketReceiveResult r;
                        do
                        {
                            r = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                            if (r.MessageType == WebSocketMessageType.Close) return;
                            ms.Write(buffer, 0, r.Count);
                        } while (!r.EndOfMessage);
                        if (r.MessageType == WebSocketMessageType.Text && gen == generation)
                        {
                            string text = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                            try { Message?.Invoke(text); }
                            catch (Exception e) { Debug.LogException(e); }
                        }
                    }
                }
            }
            catch (Exception) { /* socket closed or aborted */ }
            finally
            {
                if (gen == generation)
                {
                    Shutdown();
                    Closed?.Invoke();
                }
            }
        }

        public void Send(string text)
        {
            if (!IsOpen) return;
            outbox.Enqueue(text);
            if (!sending) _ = Pump();
        }

        async Task Pump()
        {
            sending = true;
            try
            {
                while (outbox.Count > 0 && IsOpen)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(outbox.Dequeue());
                    await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
                }
            }
            catch (Exception e) { Debug.Log("[Net] send failed: " + e.Message); }
            finally { sending = false; }
        }

        void Shutdown()
        {
            outbox.Clear();
            var socket = ws;
            var source = cts;
            ws = null;
            cts = null;
            try { source?.Cancel(); } catch (Exception) { }
            try { socket?.Abort(); } catch (Exception) { }
            try { socket?.Dispose(); } catch (Exception) { }
            try { source?.Dispose(); } catch (Exception) { }
        }

        public void Dispose()
        {
            generation++;
            Shutdown();
        }
    }
}
