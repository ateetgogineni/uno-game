using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnoGame.Core;

namespace UnoGame.EditorTools
{
    /// <summary>
    /// Engine checks run in the editor. ParityDump plays the same seeded bot games as
    /// Server/test/parity-dump.js (every mode, every bot level, 2 to 10 players); the two outputs must be identical,
    /// proving the offline C# engine follows exactly the same rules as the online server.
    /// </summary>
    public static class Checks
    {
        [MenuItem("UNO Game/Checks/Engine parity dump")]
        public static void ParityDump()
        {
            var sb = new StringBuilder();
            // 600 two-player games, then 240 games of 3 to 10 players.
            for (int seed = 1; seed <= 840; seed++)
            {
                string mode = Decks.ModeIds[seed % Decks.ModeIds.Length];
                int level = (seed / 7) % 3;
                int players = seed <= 600 ? 2 : 3 + (seed % 8);
                var g = new UnoEngine(players, seed, seed % 3 != 0, mode);
                g.Start();
                var moves = new List<string>();
                int guard = 0;
                while (!g.Over && guard++ < 5000)
                {
                    int p = g.Turn;
                    var m = UnoBot.Choose(g, p, level);
                    string err = UnoBot.Apply(g, p, m);
                    if (err != null) { moves.Add("ERR"); break; }
                    switch (m.Type)
                    {
                        case UnoBot.MoveType.Play: moves.Add($"{p}p{m.Card}c{m.Color}{(m.Uno ? "u" : "")}{(m.Target >= 0 ? "t" + m.Target : "")}"); break;
                        case UnoBot.MoveType.Roulette: moves.Add($"{p}r{m.Color}"); break;
                        case UnoBot.MoveType.Dare: moves.Add($"{p}x{(m.Ok ? 1 : 0)}"); break;
                        case UnoBot.MoveType.Draw: moves.Add($"{p}d"); break;
                        default: moves.Add($"{p}p"); break;
                    }
                }
                var counts = new List<string>();
                var outs = new StringBuilder();
                for (int i = 0; i < players; i++) { counts.Add(g.Hands[i].Count.ToString()); outs.Append(g.Out[i] ? '1' : '0'); }
                string outText = players > 2 ? " o" + outs : "";
                sb.Append($"{seed} {mode} w{g.Winner} pts{g.Points} s{g.Side} h{string.Join("/", counts)}{outText} {string.Join(" ", moves)}\n");
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/parity-cs.txt", sb.ToString());
            Debug.Log("[Checks] wrote Logs/parity-cs.txt");
            SnapshotRoundTrip();
        }

        /// <summary>
        /// Host handover: a game saved part way (through JSON, as it travels between phones) and restored
        /// must play on exactly like the original.
        /// </summary>
        [MenuItem("UNO Game/Checks/Snapshot round trip")]
        public static void SnapshotRoundTrip()
        {
            int same = 0, total = 0;
            for (int seed = 1; seed <= 300; seed++)
            {
                string mode = Decks.ModeIds[seed % Decks.ModeIds.Length];
                int players = 2 + seed % 9;
                var a = new UnoEngine(players, seed, true, mode);
                a.Start();
                int cut = 5 + seed % 60;
                for (int i = 0; i < cut && !a.Over; i++) UnoBot.Apply(a, a.Turn, UnoBot.Choose(a, a.Turn, seed % 3));
                if (a.Over) continue;
                a.TakeEvents();
                var b = UnoEngine.Restore(JsonUtility.FromJson<EngineSnapshot>(JsonUtility.ToJson(a.Snapshot())));
                string ra = Finish(a, seed % 3), rb = Finish(b, seed % 3);
                total++;
                if (ra == rb) same++;
                else Debug.LogError($"[Checks] snapshot diverged: seed {seed} {mode} {players}p\n{ra}\n{rb}");
            }
            Debug.Log($"[Checks] snapshot round trip: {same}/{total} games identical after restore");
        }

        static string Finish(UnoEngine g, int level)
        {
            var sb = new StringBuilder();
            for (int guard = 0; !g.Over && guard < 5000; guard++)
            {
                int p = g.Turn;
                var m = UnoBot.Choose(g, p, level);
                if (UnoBot.Apply(g, p, m) != null) { sb.Append("ERR"); break; }
                sb.Append(p).Append(m.Type.ToString()[0]).Append(m.Card).Append(' ');
            }
            return $"w{g.Winner} pts{g.Points} {sb}";
        }
    }
}
