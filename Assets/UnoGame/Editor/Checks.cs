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
    /// Server/test/parity-dump.js (every mode, every bot level); the two outputs must be identical,
    /// proving the offline C# engine follows exactly the same rules as the online server.
    /// </summary>
    public static class Checks
    {
        [MenuItem("UNO Game/Checks/Engine parity dump")]
        public static void ParityDump()
        {
            var sb = new StringBuilder();
            for (int seed = 1; seed <= 600; seed++)
            {
                string mode = Decks.ModeIds[seed % Decks.ModeIds.Length];
                int level = (seed / 7) % 3;
                var g = new UnoEngine(2, seed, seed % 3 != 0, mode);
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
                        case UnoBot.MoveType.Play: moves.Add($"{p}p{m.Card}c{m.Color}{(m.Uno ? "u" : "")}"); break;
                        case UnoBot.MoveType.Roulette: moves.Add($"{p}r{m.Color}"); break;
                        case UnoBot.MoveType.Dare: moves.Add($"{p}x{(m.Ok ? 1 : 0)}"); break;
                        case UnoBot.MoveType.Draw: moves.Add($"{p}d"); break;
                        default: moves.Add($"{p}p"); break;
                    }
                }
                sb.Append($"{seed} {mode} w{g.Winner} pts{g.Points} s{g.Side} h{g.Hands[0].Count}/{g.Hands[1].Count} {string.Join(" ", moves)}\n");
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/parity-cs.txt", sb.ToString());
            Debug.Log("[Checks] wrote Logs/parity-cs.txt");
        }
    }
}
