using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Gluttony
{
    public static class SongStore
    {
        private const string OwnedPrefix = "Song.Owned.";
        private const string DailyDateKey = "Song.DailyDate";
        private const string DailySongKey = "Song.DailySong";
        private const string TutorialKey = "Song.TutorialDone";
        private const string DateFormat = "yyyy-MM-dd";

        public static string RunSongId { get; set; }
        public static bool TutorialDone
        {
            get => PlayerPrefs.GetInt(TutorialKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(TutorialKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool IsOwned(Song song) => song.included || PlayerPrefs.GetInt(OwnedPrefix + song.ID, 0) == 1;

        public static bool IsDaily(SongScriptableObject library, Song song)
        {
            var daily = Daily(library);
            return daily != null && daily.ID == song.ID;
        }

        public static bool IsTrial(SongScriptableObject library, Song song) => !IsOwned(song) && IsDaily(library, song);

        public static void Buy(Song song)
        {
            PlayerPrefs.SetInt(OwnedPrefix + song.ID, 1);
            PlayerPrefs.Save();
        }

        public static Song Daily(SongScriptableObject library)
        {
            string today = DateTime.Today.ToString(DateFormat, CultureInfo.InvariantCulture);
            string id = PlayerPrefs.GetString(DailySongKey, "");
            if (PlayerPrefs.GetString(DailyDateKey, "") != today)
            {
                id = PickDaily(library, id);
                PlayerPrefs.SetString(DailyDateKey, today);
                PlayerPrefs.SetString(DailySongKey, id);
                PlayerPrefs.Save();
            }
            return Find(library, id);
        }

        public static TimeSpan TimeUntilNextDaily() => DateTime.Today.AddDays(1) - DateTime.Now;

        public static string FormatCountdown(TimeSpan time) =>
            string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds);

        public static string FormatPrice(Song song) => "US$ " + song.cost.ToString("0.00", CultureInfo.InvariantCulture);

        public static Song DefaultSong(SongScriptableObject library)
        {
            foreach (var song in library.AllSongs)
                if (song.included)
                    return song;
            return library.AllSongs[0];
        }

        public static Song RunSong(SongScriptableObject library) => Find(library, RunSongId) ?? DefaultSong(library);

        public static string Status(SongScriptableObject library, Song song)
        {
            if (IsOwned(song))
                return "(RÉCORD " + PlayerPrefs.GetInt(ScoreManager.BestKeyFor(song.ID), 0) + ")";
            if (IsDaily(library, song))
                return "(PRUEBA " + FormatCountdown(TimeUntilNextDaily()) + ")";
            return "(" + FormatPrice(song) + ")";
        }

        public static void Forget(SongScriptableObject library)
        {
            foreach (var song in library.AllSongs)
            {
                PlayerPrefs.DeleteKey(OwnedPrefix + song.ID);
                PlayerPrefs.DeleteKey(ScoreManager.BestKeyFor(song.ID));
            }
            PlayerPrefs.DeleteKey(DailyDateKey);
            PlayerPrefs.DeleteKey(DailySongKey);
            PlayerPrefs.DeleteKey(TutorialKey);
            PlayerPrefs.Save();
        }

        public static Song Find(SongScriptableObject library, string id)
        {
            foreach (var song in library.AllSongs)
                if (song.ID == id)
                    return song;
            return null;
        }

        private static string PickDaily(SongScriptableObject library, string previousId)
        {
            var locked = new List<Song>();
            foreach (var song in library.AllSongs)
                if (!IsOwned(song))
                    locked.Add(song);
            if (locked.Count > 1)
                locked.RemoveAll(song => song.ID == previousId);
            return locked.Count > 0 ? locked[UnityEngine.Random.Range(0, locked.Count)].ID : "";
        }
    }
}
