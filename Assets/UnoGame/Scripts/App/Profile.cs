using System;
using UnityEngine;

namespace UnoGame
{
    /// <summary>Local player profile and stats, stored in PlayerPrefs.</summary>
    [Serializable]
    public sealed class Profile
    {
        public string pid;
        public string name;
        public int avatar;
        public int wins;
        public int games;
        public int streak;
        public int bestStreak;
        public int points;
        public bool sound = true;
        public string recent = "";   // last results, newest last: "WLW..."
        public int lossStreak;

        [NonSerialized] string key;

        public static Profile Load()
        {
            string key = "uno.profile";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-profile") key += "." + args[i + 1];

            Profile p = null;
            string json = PlayerPrefs.GetString(key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { p = JsonUtility.FromJson<Profile>(json); } catch (Exception) { p = null; }
            }
            if (p == null) p = new Profile();
            p.key = key;
            if (string.IsNullOrEmpty(p.pid)) p.pid = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(p.name)) p.name = "Player" + UnityEngine.Random.Range(1000, 9999);
            p.avatar = Mathf.Clamp(p.avatar, 0, Net.Avatars.Count - 1);
            if (string.IsNullOrEmpty(json)) p.avatar = UnityEngine.Random.Range(0, Net.Avatars.Count);
            p.Save();
            return p;
        }

        public void Save()
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void RecordGame(bool won, int pts)
        {
            games++;
            if (won)
            {
                wins++;
                streak++;
                lossStreak = 0;
                bestStreak = Mathf.Max(bestStreak, streak);
                points += pts;
            }
            else
            {
                streak = 0;
                lossStreak++;
            }
            recent = ((recent ?? "") + (won ? "W" : "L"));
            if (recent.Length > 6) recent = recent.Substring(recent.Length - 6);
            Save();
        }

        /// <summary>
        /// Bot difficulty for this player (0 easy, 1 normal, 2 hard). New players get the easy bot;
        /// after that it follows their last few games, and two losses in a row always drop it to easy.
        /// </summary>
        public int BotLevel
        {
            get
            {
                if (games < 5 || lossStreak >= 2) return 0;
                int w = 0;
                foreach (char c in recent ?? "") if (c == 'W') w++;
                float rate = string.IsNullOrEmpty(recent) ? 0.5f : w / (float)recent.Length;
                if (rate < 0.45f) return 0;
                if (rate < 0.75f) return 1;
                return 2;
            }
        }

        /// <summary>Names other players will see: letters, digits, _ . - and spaces; 2-14 characters.</summary>
        public static string CleanName(string raw)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in raw ?? "")
            {
                if (char.IsLetterOrDigit(c) && c < 128 || c == '_' || c == '.' || c == '-' || c == ' ') sb.Append(c);
                if (sb.Length >= 14) break;
            }
            string s = sb.ToString().Trim();
            return s.Length >= 2 ? s : null;
        }
    }
}
