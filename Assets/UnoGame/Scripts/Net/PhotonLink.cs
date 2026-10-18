using System;
using System.Collections.Generic;
using Photon.Client;
using Photon.Realtime;
using UnoGame.Core;
using UnityEngine;

namespace UnoGame.Net
{
    /// <summary>
    /// Online play through Photon Cloud, with no game server of our own. Pressing Play joins (or opens) a
    /// room for the same mode and table size; after a few seconds the room's master client fills the empty
    /// seats with bots, sends everyone the seating and runs the table (PhotonSession + TableHost).
    /// </summary>
    public sealed class PhotonLink : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks, IInRoomCallbacks, IOnEventCallback
    {
        public const byte EvToHost = 1, EvToSeat = 2, EvMatched = 3, EvSnapshot = 4;
        const string PropMode = "m", PropAvatar = "a", PropLevel = "l";
        const int PlayerTtlMs = 20000;

        RealtimeClient client;
        GameConfig cfg;
        Profile profile;
        Matchmaker owner;
        bool connecting;
        bool searching;
        string findMode = "classic";
        int findSize = 2;
        float startAt = -1f;
        PhotonSession session;
        float reconnectGiveUp = -1f;

        public bool Ready => client != null && client.IsConnectedAndReady;
        public bool Connecting => connecting;
        public int OnlineCount => client == null ? -1 : client.PlayersOnMasterCount + client.PlayersInRoomsCount;

        static float Now => Time.unscaledTime;

        public void Init(GameConfig config, Profile prof, Matchmaker mm)
        {
            cfg = config;
            profile = prof;
            owner = mm;
        }

        // ---------------------------------------------------------------- connection

        public void Connect()
        {
            if (client == null)
            {
                client = new RealtimeClient();
                client.AddCallbackTarget(this);
            }
            if (client.IsConnected || connecting) return;
            connecting = true;
            client.NickName = profile.name;
            client.AuthValues = new AuthenticationValues(profile.pid);
            var settings = new AppSettings
            {
                AppIdRealtime = cfg.photonAppId,
                AppVersion = Application.version,      // only the same app version plays together
                FixedRegion = string.IsNullOrEmpty(cfg.photonRegion) ? null : cfg.photonRegion,
                EnableProtocolFallback = true,
            };
            Debug.Log("[Photon] connecting" + (settings.FixedRegion != null ? " to region " + settings.FixedRegion : " (closest region)"));
            if (!client.ConnectUsingSettings(settings)) Failed("could not start connecting");
        }

        void Failed(string why)
        {
            Debug.Log("[Photon] " + why);
            connecting = false;
            if (searching)
            {
                searching = false;
                owner.PhotonSearchFailed();
            }
            owner.PhotonStatusChanged();
        }

        void Update()
        {
            client?.Service();
            if (client == null) return;

            // master client of a room still being filled: start once it's full or the wait is over
            if (searching && session == null && client.InRoom && client.LocalPlayer.IsMasterClient)
            {
                var room = client.CurrentRoom;
                if (startAt < 0f) startAt = Now + cfg.botAfterSec + UnityEngine.Random.Range(0f, 2f);
                if (room.PlayerCount >= room.MaxPlayers || Now >= startAt) StartTable();
            }

            if (reconnectGiveUp > 0f && Now > reconnectGiveUp)
            {
                reconnectGiveUp = -1f;
                session?.ConnectionLost();
            }
        }

        // ---------------------------------------------------------------- matchmaking

        public void Find(string mode, int size)
        {
            findMode = mode;
            findSize = size;
            searching = true;
            startAt = -1f;
            if (client == null || !client.IsConnected) { Connect(); return; }
            if (client.InRoom) { client.OpLeaveRoom(false); return; }   // back on the master server, OnConnectedToMaster joins
            if (Ready) JoinOrCreate();
            // otherwise still connecting: OnConnectedToMaster joins
        }

        void JoinOrCreate()
        {
            client.LocalPlayer.SetCustomProperties(new PhotonHashtable { { PropAvatar, profile.avatar }, { PropLevel, profile.BotLevel } });
            var props = new PhotonHashtable { { PropMode, findMode } };
            Debug.Log($"[Photon] looking for a {findMode} table of {findSize}");
            client.OpJoinRandomOrCreateRoom(
                new JoinRandomRoomArgs { ExpectedCustomRoomProperties = props, ExpectedMaxPlayers = findSize },
                new EnterRoomArgs
                {
                    RoomOptions = new RoomOptions
                    {
                        MaxPlayers = findSize,
                        CustomRoomProperties = props,
                        CustomRoomPropertiesForLobby = new object[] { PropMode },
                        PlayerTtl = PlayerTtlMs,
                        EmptyRoomTtl = 0,
                    },
                });
        }

        public void Cancel()
        {
            searching = false;
            if (session == null && client != null && client.InRoom) client.OpLeaveRoom(false);
        }

        /// <summary>Master client: seat everyone who's here, fill the rest with bots and start.</summary>
        void StartTable()
        {
            var room = client.CurrentRoom;
            room.IsOpen = false;
            room.IsVisible = false;

            var humans = new List<Player>();
            foreach (var p in room.Players.Values) if (!p.IsInactive) humans.Add(p);
            humans.Sort((a, b) => a.ActorNumber.CompareTo(b.ActorNumber));

            int size = Mathf.Clamp(room.MaxPlayers, Matchmaker.MinPlayers, Matchmaker.MaxPlayers);
            var players = new List<PlayerInfo>();
            var actors = new List<int>();
            var levels = new List<int>();
            var names = new List<string>();
            var avatars = new List<int>();
            int botLevel = UnoBot.Hard;
            foreach (var p in humans)
            {
                if (players.Count >= size) break;
                int avatar = p.CustomProperties.TryGetValue(PropAvatar, out var a) && a is int ai ? ai : 0;
                int level = p.CustomProperties.TryGetValue(PropLevel, out var l) && l is int li ? li : UnoBot.Normal;
                botLevel = Mathf.Min(botLevel, level);
                string name = string.IsNullOrEmpty(p.NickName) ? "Player" : p.NickName;
                players.Add(new PlayerInfo { name = name, avatar = avatar });
                actors.Add(p.ActorNumber);
                levels.Add(level);
                names.Add(name);
                avatars.Add(avatar);
            }
            while (players.Count < size)
            {
                int avatar;
                do { avatar = UnityEngine.Random.Range(0, Avatars.Count); } while (avatars.Contains(avatar) && avatars.Count < Avatars.Count);
                string name = BotNames.Pick(names);
                players.Add(new PlayerInfo { name = name, avatar = avatar });
                actors.Add(0);
                levels.Add(botLevel);
                names.Add(name);
                avatars.Add(avatar);
            }
            if (size > 2)
            {
                // random seating, so friends who queue together don't always sit side by side
                for (int i = players.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    (players[i], players[j]) = (players[j], players[i]);
                    (actors[i], actors[j]) = (actors[j], actors[i]);
                    (levels[i], levels[j]) = (levels[j], levels[i]);
                }
            }
            var table = new TableInfo { match = room.Name, mode = findMode, players = players.ToArray(), actors = actors.ToArray(), levels = levels.ToArray() };
            Debug.Log($"[Photon] starting {findMode} table {room.Name}: {humans.Count} people, {size - humans.Count} bots");
            client.OpRaiseEvent(EvMatched, JsonUtility.ToJson(table), new RaiseEventArgs { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
            Matched(table, true);
        }

        void Matched(TableInfo table, bool isHost)
        {
            if (!searching || session != null) return;
            if (Array.IndexOf(table.actors, client.LocalPlayer.ActorNumber) < 0) return;
            searching = false;
            session = new PhotonSession(this, cfg, table, client.LocalPlayer.ActorNumber, isHost);
            owner.PhotonMatched(session);
        }

        // ---------------------------------------------------------------- messages

        public bool IsInRoom(int actor)
        {
            if (client == null || !client.InRoom) return false;
            return client.CurrentRoom.Players.TryGetValue(actor, out var p) && !p.IsInactive;
        }

        public void SendTo(int actor, byte code, string json)
        {
            if (client == null || !client.InRoom) return;
            client.OpRaiseEvent(code, json, new RaiseEventArgs { TargetActors = new[] { actor } }, SendOptions.SendReliable);
        }

        public void SendToOthers(byte code, string json)
        {
            if (client == null || !client.InRoom || client.CurrentRoom.PlayerCount <= 1) return;
            client.OpRaiseEvent(code, json, new RaiseEventArgs { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        }

        public void SendToHost(byte code, string json)
        {
            if (client == null || !client.InRoom) return;
            SendTo(client.CurrentRoom.MasterClientId, code, json);
        }

        /// <summary>The match is over or abandoned: leave its room (stay connected for the next one).</summary>
        public void EndSession(PhotonSession s)
        {
            if (session != s) return;
            session = null;
            reconnectGiveUp = -1f;
            if (client != null && client.InRoom) client.OpLeaveRoom(false);
        }

        public void OnEvent(EventData ev)
        {
            if (ev.Code > EvSnapshot || ev.Code < EvToHost) return;
            if (!(ev.CustomData is string json)) return;
            if (ev.Code == EvMatched)
            {
                var table = JsonUtility.FromJson<TableInfo>(json);
                if (table != null) Matched(table, false);
                return;
            }
            session?.OnEvent(ev.Code, ev.Sender, json);
        }

        // ---------------------------------------------------------------- Photon callbacks

        public void OnConnected() { }

        public void OnConnectedToMaster()
        {
            connecting = false;
            Debug.Log("[Photon] connected, region " + client.CurrentRegion);
            owner.PhotonStatusChanged();
            if (searching && session == null) JoinOrCreate();
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            Debug.Log("[Photon] disconnected: " + cause);
            bool wasConnecting = connecting;
            connecting = false;
            if (session != null && !session.Finished)
            {
                // try to get back into the same room; the host keeps our seat for a while
                session.ConnectionDropped();
                if (reconnectGiveUp < 0f) reconnectGiveUp = Now + 25f;
                if (!client.ReconnectAndRejoin()) Invoke(nameof(RetryRejoin), 2f);
                return;
            }
            if (searching) Failed(wasConnecting ? "could not connect: " + cause : "lost the connection while searching");
            else owner.PhotonStatusChanged();
        }

        void RetryRejoin()
        {
            if (session == null || session.Finished || client.IsConnected) return;
            if (!client.ReconnectAndRejoin()) Invoke(nameof(RetryRejoin), 3f);
        }

        public void OnRegionListReceived(RegionHandler regionHandler) { }
        public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
        public void OnCustomAuthenticationFailed(string debugMessage) => Failed("authentication failed: " + debugMessage);

        public void OnFriendListUpdate(List<FriendInfo> friendList) { }
        public void OnCreatedRoom() => Debug.Log("[Photon] opened a new table, waiting for players");
        public void OnCreateRoomFailed(short returnCode, string message) => Failed("could not open a table: " + message);
        public void OnJoinRoomFailed(short returnCode, string message)
        {
            if (session != null) { reconnectGiveUp = -1f; session.ConnectionLost(); return; }
            Failed("could not join: " + message);
        }
        public void OnJoinRandomFailed(short returnCode, string message) => Failed("could not join: " + message);

        public void OnJoinedRoom()
        {
            if (session != null)
            {
                Debug.Log("[Photon] back in the match");
                reconnectGiveUp = -1f;
                session.ConnectionBack();
                return;
            }
            startAt = -1f;
            Debug.Log($"[Photon] joined table {client.CurrentRoom.Name} ({client.CurrentRoom.PlayerCount}/{client.CurrentRoom.MaxPlayers})");
        }

        public void OnLeftRoom() { }

        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (session != null) session.PlayerReturned(newPlayer);
            else Debug.Log($"[Photon] {newPlayer.NickName} joined the table");
        }

        public void OnPlayerLeftRoom(Player otherPlayer) => session?.PlayerLeft(otherPlayer);

        public void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged) { }
        public void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps) { }

        public void OnMasterClientSwitched(Player newMasterClient)
        {
            if (session != null) session.MasterSwitched(newMasterClient);
            else startAt = -1f;   // a new master restarts the wait for players
        }

        void OnDestroy()
        {
            if (client == null) return;
            client.RemoveCallbackTarget(this);
            if (client.IsConnected) client.Disconnect();
        }
    }
}
