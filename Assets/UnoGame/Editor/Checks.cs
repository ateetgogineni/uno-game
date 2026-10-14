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
    /// Server/test/parity-dump.js; the two outputs must be identical, proving the offline C# engine
    /// follows exactly the same rules as the online server.
    /// </summary>
    public static class Checks
    {
        [MenuItem("UNO Game/Checks/Engine parity dump")]
        public static void ParityDump()
        {
            var sb = new StringBuilder();
            for (int seed = 1; seed <= 500; seed++)
            {
                var g = new UnoEngine(2, seed, seed % 3 != 0);
                g.Start();
                var moves = new List<string>();
                int guard = 0;
                while (!g.Over && guard++ < 5000)
                {
                    int p = g.Turn;
                    var m = UnoBot.Choose(g, p);
                    string err = UnoBot.Apply(g, p, m);
                    if (err != null) { moves.Add("ERR"); break; }
                    if (m.Type == UnoBot.MoveType.Play) moves.Add($"{p}p{m.Card}c{m.Color}{(m.Uno ? "u" : "")}");
                    else moves.Add($"{p}{(m.Type == UnoBot.MoveType.Draw ? "d" : "p")}");
                }
                sb.Append($"{seed} w{g.Winner} pts{g.Points} h{g.Hands[0].Count}/{g.Hands[1].Count} {string.Join(" ", moves)}\n");
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/parity-cs.txt", sb.ToString());
            Debug.Log("[Checks] wrote Logs/parity-cs.txt");
        }
    }
}
