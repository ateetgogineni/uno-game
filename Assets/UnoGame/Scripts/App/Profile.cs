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
                bestStreak = Mathf.Max(bestStreak, streak);
                points += pts;
            }
            else streak = 0;
            Save();
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
