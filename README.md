# UNO Game

1v1 UNO for Android (Unity 6000.3.26f1) with six game modes, modelled on the Plato-style reference video:
home screen with a mode list, a rules screen per mode, matchmaking over a WebSocket, a server-run game
with bots filling in when nobody else is searching, and an offline bot match when the server can't be reached.

## Game modes

| Mode | Deck | Rules |
|---|---|---|
| **Classic UNO** | 108 | Official rules; Wild Draw 4 only without a card of the current colour; +2/+4 stacking (as in the reference video) |
| **UNO Flip** | 112, double-sided | FLIP turns hands, draw pile and discard pile over. Light side: Draw 1, Wild Draw 2. Dark side (pink/teal/orange/purple): Draw 5, Skip Everyone, Wild Draw Color. You see the other side of the opponent's cards and of the draw pile. |
| **Show 'Em No Mercy** | 168 | Stack any draw card of equal or higher value (+2, +4, +6, +10); draw until you can play; 25 cards = out (Mercy rule); 7 and 0 swap hands; Discard All, Skip Everyone, Wild Reverse Draw 4, Color Roulette |
| **UNO All Wild** | 112, all wild | Any card on any card: Wild Skip, Skip Two, Reverse, Draw 2/4, Targeted Draw 2, Forced Swap |
| **UNO Attack** | 112 | The card launcher replaces the draw pile (fires nothing or a burst of cards); Hit 2, Wild All Hit, Wild Hit-Fire, Discard All, Trade Hands |
| **UNO Dare** | 108 | Dare / Wild Dare: do a dare or draw 2/4. Dares are on-screen challenges (tap frenzy, reflex, colour trap, hold steady); success keeps your turn |

All modes: 7 cards each, Skip/Reverse give another turn with two players, forgetting to tap **UNO!**
before your second-last card can cost 2 cards (bots only catch it some of the time), first to empty their
hand wins the opponent's card points. A game where nobody can play any more (empty deck) goes to the
fewest cards. Bots play at easy/normal/hard; new players get the easy bot and it then adapts to results.
Mode rules live in `Server/src/cards.js` (MODES) and `Assets/UnoGame/Scripts/Core/Decks.cs`.

## Layout

| Path | What it is |
|---|---|
| `Assets/UnoGame/Scripts` | Game code: `Core` (decks, rules engine, bot), `Net` (WebSocket client, matchmaker, online/offline sessions), `Game` (table, animations, dares), `Home` (home, mode rules, matchmaking), `Dev` (autopilot tests, layout audit, video capture) |
| `Assets/UnoGame/Scripts/App/ModeInfo.cs` | Mode list, taglines and the rules shown before matchmaking |
| `Assets/UnoGame/Editor` | `Pipeline` (settings, builds), `ArtImporter`, `SceneBuilder` (scenes, card prefab, matchmaking Animator), `Checks` |
| `Assets/UnoGame/Resources/GameConfig.asset` | Server address, timeouts, turn length |
| `Server/` | Node matchmaking + game server (no dependencies), tests, Dockerfile |
| `Tools/art/generate.js` | Draws all production art (155 cards, UI, avatars) as SVG and renders the atlases with headless Edge |
| `Keystore/` | Android upload key (**git-ignored, back it up**) |

## Build

```
Unity -batchmode -quit -projectPath . -executeMethod UnoGame.EditorTools.Pipeline.Prepare        # font, art, scenes
Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod UnoGame.EditorTools.Pipeline.BuildAndroid
```

Outputs `Builds/Android/UNO Game-<version>.apk` (install directly) and `.aab` (Google Play), signed with
`Keystore/unogame-upload.keystore`. Bump `Version`/`VersionCode` in `Pipeline.cs` for each release.

## Server

```
cd Server && npm test                       # rules for every mode, bot games, real WebSocket matches
docker compose -f Server/docker-compose.yml up -d --build
```

Players are matched only within the same mode. The app connects to `serverUrl` in `GameConfig.asset`
(currently `ws://192.168.29.30:8090/`, home network only). For players anywhere, host the server at a
public `wss://` address and update that field.

## Testing

`Builds/Windows/UNO Game.exe -autopilot -games 6 -modes all -shots <dir>` plays one game of every mode
by itself, saves screenshots and logs a `[LAYOUT]` overlap/off-screen report per screen.
`-capture <dir> -supersize 2` records frames + audio for a video.
`Checks.ParityDump` vs `node Server/test/parity-dump.js` proves the offline C# engine plays every mode
exactly like the server (600 seeded games).
