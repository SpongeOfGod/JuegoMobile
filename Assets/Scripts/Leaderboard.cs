using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Gluttony
{
    public struct LeaderboardEntry
    {
        public string Id;
        public string Name;
        public int Score;
    }

    public class Leaderboard : MonoBehaviour
    {
        private const string IdKey = "Online.PlayerId";
        private const string NameKey = "Online.Name";
        private const string QueueKey = "Online.Queue";
        private const string IdChars = "abcdefghijklmnopqrstuvwxyz0123456789";
        private const int IdLength = 6;
        private const int MaxQueued = 50;
        private const int TimeoutSeconds = 10;
        public const int TopCount = 5;
        public const int MaxNameLength = 8;

        private static readonly Regex EntryPattern = new Regex("\\{([^{}]*)\\}");
        private static readonly Regex IdPattern = new Regex("\"id\"\\s*:\\s*\"([^\"]*)\"");
        private static readonly Regex NamePattern = new Regex("\"name\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        private static readonly Regex ScorePattern = new Regex("\"score\"\\s*:\\s*(\\d+)");
        private static readonly List<UnityWebRequest> Sending = new List<UnityWebRequest>();

        [SerializeField] private string scriptUrl = "";

        private readonly Dictionary<string, List<LeaderboardEntry>> cache = new Dictionary<string, List<LeaderboardEntry>>();
        private readonly HashSet<string> failedSongs = new HashSet<string>();
        private readonly HashSet<string> busySongs = new HashSet<string>();

        public event Action<string> Updated;

        private struct QueuedRun
        {
            public string Song;
            public int Score;
            public string Token;

            public override string ToString() => Song + ":" + Score.ToString(CultureInfo.InvariantCulture) + ":" + Token;
        }

        public static string PlayerId
        {
            get
            {
                string id = PlayerPrefs.GetString(IdKey, "");
                if (id.Length == 0)
                {
                    id = RandomId();
                    PlayerPrefs.SetString(IdKey, id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }

        public static string PlayerName
        {
            get
            {
                string name = PlayerPrefs.GetString(NameKey, "");
                if (name.Length == 0)
                {
                    name = "GATO" + UnityEngine.Random.Range(1000, 10000).ToString(CultureInfo.InvariantCulture);
                    PlayerPrefs.SetString(NameKey, name);
                    PlayerPrefs.Save();
                }
                return name;
            }
            set
            {
                string clean = CleanName(value);
                if (clean.Length == 0)
                    return;
                PlayerPrefs.SetString(NameKey, clean);
                PlayerPrefs.Save();
            }
        }

        public static string CleanName(string value)
        {
            var builder = new StringBuilder();
            foreach (char c in (value ?? "").ToUpperInvariant())
            {
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == 'Ñ')
                    builder.Append(c);
                if (builder.Length >= MaxNameLength)
                    break;
            }
            return builder.ToString();
        }

        private static string RandomId()
        {
            var random = new System.Random(Guid.NewGuid().GetHashCode());
            var builder = new StringBuilder();
            for (int i = 0; i < IdLength; i++)
                builder.Append(IdChars[random.Next(IdChars.Length)]);
            return builder.ToString();
        }

        private static List<QueuedRun> LoadQueue()
        {
            var runs = new List<QueuedRun>();
            foreach (string item in PlayerPrefs.GetString(QueueKey, "").Split(';'))
            {
                string[] parts = item.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int score))
                    runs.Add(new QueuedRun { Song = parts[0], Score = score, Token = parts[2] });
            }
            return runs;
        }

        private static void SaveQueue(List<QueuedRun> runs)
        {
            while (runs.Count > MaxQueued)
                runs.RemoveAt(0);
            PlayerPrefs.SetString(QueueKey, string.Join(";", runs));
            PlayerPrefs.Save();
        }

        private static void Dequeue(string token)
        {
            var runs = LoadQueue();
            runs.RemoveAll(run => run.Token == token);
            SaveQueue(runs);
        }

        public void Queue(string songId, int score)
        {
            if (score <= 0)
                return;
            var runs = LoadQueue();
            runs.Add(new QueuedRun { Song = songId, Score = score, Token = RandomId() });
            SaveQueue(runs);
            Updated?.Invoke(songId);
        }

        private bool Configured => scriptUrl.StartsWith("https://script.google.com/", StringComparison.Ordinal);

        public bool IsBusy(string songId) => busySongs.Contains(songId);

        public bool HasFailed(string songId) => failedSongs.Contains(songId);

        public List<LeaderboardEntry> Entries(string songId)
        {
            if (!cache.TryGetValue(songId, out var cached))
                return null;
            var entries = new List<LeaderboardEntry>(cached);
            foreach (var run in LoadQueue())
                if (run.Song == songId)
                    entries.Add(new LeaderboardEntry { Id = PlayerId, Name = PlayerName, Score = run.Score });
            return Sorted(entries);
        }

        public void Sync(string songId, bool send)
        {
            StopAllCoroutines();
            busySongs.Clear();
            busySongs.Add(songId);
            Updated?.Invoke(songId);
            StartCoroutine(SyncRoutine(songId, send));
        }

        public void Push()
        {
            if (!Configured)
                return;
            foreach (var run in LoadQueue())
            {
                string token = run.Token;
                var request = UnityWebRequest.Get(BuildUrl(run.Song, run.Score));
                request.timeout = TimeoutSeconds;
                Sending.Add(request);
                request.SendWebRequest().completed += _ =>
                {
                    if (Succeeded(request))
                        Dequeue(token);
                    Sending.Remove(request);
                    request.Dispose();
                };
            }
        }

        private void Finish(string songId, List<LeaderboardEntry> entries)
        {
            busySongs.Remove(songId);
            if (entries != null)
            {
                cache[songId] = entries;
                failedSongs.Remove(songId);
            }
            else
            {
                failedSongs.Add(songId);
            }
            Updated?.Invoke(songId);
        }

        private string BuildUrl(string songId, int score)
        {
            var url = new StringBuilder(scriptUrl);
            url.Append(scriptUrl.Contains("?") ? '&' : '?');
            url.Append("song=").Append(UnityWebRequest.EscapeURL(songId));
            if (score > 0)
            {
                url.Append("&id=").Append(PlayerId);
                url.Append("&name=").Append(UnityWebRequest.EscapeURL(PlayerName));
                url.Append("&score=").Append(score.ToString(CultureInfo.InvariantCulture));
            }
            return url.ToString();
        }

        private static bool Succeeded(UnityWebRequest request) =>
            request.result == UnityWebRequest.Result.Success && request.downloadHandler.text.Contains("\"ok\":true");

        private IEnumerator SyncRoutine(string songId, bool send)
        {
            if (!Configured || Application.internetReachability == NetworkReachability.NotReachable)
            {
                Finish(songId, null);
                yield break;
            }

            string latest = null;
            if (send)
            {
                foreach (var run in LoadQueue())
                {
                    using (var request = UnityWebRequest.Get(BuildUrl(run.Song, run.Score)))
                    {
                        request.timeout = TimeoutSeconds;
                        yield return request.SendWebRequest();
                        if (!Succeeded(request))
                            break;
                        Dequeue(run.Token);
                        if (run.Song == songId)
                            latest = request.downloadHandler.text;
                    }
                }
            }

            if (latest == null)
            {
                using (var request = UnityWebRequest.Get(BuildUrl(songId, 0)))
                {
                    request.timeout = TimeoutSeconds;
                    yield return request.SendWebRequest();
                    if (Succeeded(request))
                        latest = request.downloadHandler.text;
                }
            }
            Finish(songId, latest != null ? Parse(latest) : null);
        }

        private static List<LeaderboardEntry> Sorted(List<LeaderboardEntry> entries) =>
            entries.OrderByDescending(entry => entry.Score).ToList();

        private static List<LeaderboardEntry> Parse(string json)
        {
            var entries = new List<LeaderboardEntry>();
            foreach (Match match in EntryPattern.Matches(json))
            {
                string body = match.Groups[1].Value;
                var id = IdPattern.Match(body);
                var name = NamePattern.Match(body);
                var score = ScorePattern.Match(body);
                if (!id.Success || !name.Success || !score.Success || !int.TryParse(score.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int points))
                    continue;
                string clean = CleanName(Regex.Unescape(name.Groups[1].Value));
                entries.Add(new LeaderboardEntry { Id = id.Groups[1].Value, Name = clean.Length > 0 ? clean : "???", Score = points });
            }
            entries = Sorted(entries);
            if (entries.Count > TopCount)
                entries.RemoveRange(TopCount, entries.Count - TopCount);
            return entries;
        }
    }
}
