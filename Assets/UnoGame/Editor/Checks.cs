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
        }
    }
}
