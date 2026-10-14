# UNO Game

1v1 UNO for Android (Unity 6000.3.26f1), modelled on the Plato-style reference video: home screen,
matchmaking over a WebSocket, a server-run game with bots filling in when nobody else is searching,
and an offline bot match when the server can't be reached.

## Rules

Official Mattel rules for two players, plus +2/+4 stacking as in the reference video:
108-card deck, 7 cards each, first discard is always a number card; Wild Draw 4 only when you hold no
card of the current colour; Skip and Reverse give you another turn; forgetting to tap **UNO!** before
playing your second-last card costs 2 cards; draw one card when you can't (or don't want to) play and
play it right away or pass; first to empty their hand wins the opponent's card points.
Stacking is one switch: `STACKING` on the server and `GameConfig.stacking` in Unity.

## Layout

| Path | What it is |
|---|---|
| `Assets/UnoGame/Scripts` | Game code: `Core` (rules engine, bot), `Net` (WebSocket client, matchmaker, online/offline sessions), `Game` (table, animations), `Home` (home + matchmaking), `Dev` (autopilot tests, layout audit, video capture) |
| `Assets/UnoGame/Editor` | `Pipeline` (settings, builds), `ArtImporter`, `SceneBuilder` (scenes, card prefab, matchmaking Animator), `Checks` |
| `Assets/UnoGame/Resources/GameConfig.asset` | Server address, timeouts, turn length |
| `Server/` | Node matchmaking + game server (no dependencies), tests, Dockerfile |
| `Tools/art/generate.js` | Draws all production art as SVG and renders the atlases with headless Edge |
| `Keystore/` | Android upload key (**git-ignored, back it up**) |

## Build

```
Unity -batchmode -quit -projectPath . -executeMethod UnoGame.EditorTools.Pipeline.Prepare        # font, art, scenes
Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod UnoGame.EditorTools.Pipeline.BuildAndroid
```

Outputs `Builds/Android/UNO Game-1.0.0.apk` (install directly) and `.aab` (Google Play), signed with
`Keystore/unogame-upload.keystore`. Bump `Version`/`VersionCode` in `Pipeline.cs` for each release.

## Server

```
cd Server && npm test                       # rules, 3000 simulated games, real WebSocket matches
docker compose -f Server/docker-compose.yml up -d --build
```

The app connects to `serverUrl` in `GameConfig.asset` (currently `ws://192.168.29.30:8090/`, home
network only). For players anywhere, host the server at a public `wss://` address and update that field.

## Testing

`Builds/Windows/UNO Game.exe -autopilot -games 1 -shots <dir>` plays a game by itself, saves screenshots
and logs a `[LAYOUT]` overlap/off-screen report. `-capture <dir> -supersize 2` records frames + audio for a video.
`Checks.ParityDump` vs `node Server/test/parity-dump.js` proves the offline C# engine matches the server.
