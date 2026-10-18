# UNO Game

UNO for Android (Unity 6000.3.26f1) for 2 to 10 players with six game modes, modelled on the Plato-style
reference video: home screen with a mode list, a rules screen per mode (where you pick the table size),
matchmaking over a WebSocket, a server-run game with bots filling the empty seats, and an offline bot game
when the server can't be reached.

## Tables

| Players | Screen | Layout |
|---|---|---|
| 2 | portrait | the opponent's fan across the top (as in the reference video) |
| 3 - 4 | portrait | compact seats beside and above the table |
| 5 - 10 | landscape | seats spread along an arc over the table; chevrons on the arc show the direction of play |

The phone turns to landscape behind the loading screen and back to portrait on the way home. At 3+
players: Reverse turns play round (no skip), Skip Everyone skips every opponent, Swap / Trade Hands /
the No Mercy 7 / Targeted Draw let you pick the player, the No Mercy 0 passes every hand on, the 25-card
Mercy rule knocks out just that player (their cards go back under the draw pile), and a player who leaves
is taken out while the rest play on.

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
| `Assets/UnoGame/Scripts` | Game code: `Core` (decks, rules engine, bot), `Net` (Photon link, table host, WebSocket client, matchmaker, sessions), `Game` (table, animations, dares), `Home` (home, mode rules, matchmaking), `Dev` (autopilot tests, layout audit, video capture) |
| `Assets/UnoGame/Scripts/App/ModeInfo.cs` | Mode list, taglines and the rules shown before matchmaking |
| `Assets/UnoGame/Editor` | `Pipeline` (settings, builds), `ArtImporter`, `SceneBuilder` (scenes, card prefab, matchmaking Animator), `Checks` |
| `Assets/UnoGame/Resources/GameConfig.asset` | Server address, timeouts, turn length |
| `Server/` | Optional Node matchmaking + game server (no dependencies), tests, Dockerfile |
| `Assets/Photon` | Photon Realtime Unity SDK v5.1.20 (client library + Realtime) |
| `Tools/art/generate.js` | Draws all production art (155 cards, UI, avatars) as SVG and renders the atlases with headless Edge |
| `Keystore/` | Android upload key (**git-ignored, back it up**) |

## Build

```
Unity -batchmode -quit -projectPath . -executeMethod UnoGame.EditorTools.Pipeline.Prepare        # font, art, scenes
Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod UnoGame.EditorTools.Pipeline.BuildAndroid
```

Outputs `Builds/Android/UNO Game-<version>.apk` (install directly) and `.aab` (Google Play), signed with
`Keystore/unogame-upload.keystore`. Bump `Version`/`VersionCode` in `Pipeline.cs` for each release.

## Online play (Photon Cloud, no server to host)

Online games go through Photon Realtime (`Assets/Photon`, SDK v5.1.20; App ID and region in
`GameConfig.asset`). Pressing Play joins or opens a Photon room for the same mode and table size; after
a few seconds the room's master client fills the empty seats with bots and runs the table on that phone
(`Net/TableHost.cs`, a port of the server's match logic, so the game screen can't tell the difference).
Each player only receives their own hand. The host shares a game snapshot after every move, so if it
leaves, the next phone takes over the same game. Only players on the same app version meet. No network
or no Photon: the game falls back to an offline bot game.

## Server (optional)

```
cd Server && npm test                       # rules for every mode, bot games, real WebSocket matches
docker compose -f Server/docker-compose.yml up -d --build
```

Set `network` to Server in `GameConfig.asset` (or start the app with `-server ws://host:port/`) to use
the Node server instead of Photon. Players are matched only within the same mode and table size; bots
fill the empty seats after a few seconds. The server is authoritative (no phone sees other hands).

## Testing

`Builds/Windows/UNO Game.exe -autopilot -games 6 -modes all -players 2,5,10 -shots <dir>` plays one game of every mode
by itself, saves screenshots and logs a `[LAYOUT]` overlap/off-screen report per screen.
`-capture <dir> -supersize 2` records frames + audio for a video.
`Checks.ParityDump` vs `node Server/test/parity-dump.js` proves the offline C# engine plays every mode
exactly like the server (840 seeded games, 2 to 10 players).
