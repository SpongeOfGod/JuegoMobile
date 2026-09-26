using System;
using UnityEngine;

namespace Gluttony
{
    [DefaultExecutionOrder(-100)]
    public class Conductor : MonoBehaviour
    {
        private const float PulseBeats = 0.3f;
        private const double MaxDriftSeconds = 0.1;
        private const double ResyncSeconds = 0.25;

        public static Conductor Instance { get; private set; }

        [SerializeField] private float bpm = 120f;
        [SerializeField] private float firstBeatSeconds;
        [SerializeField] private float outputLatencyMs;
        [SerializeField] private MusicPlayer music;

        public event Action<int> Beat;

        public float Bpm => bpm;
        public double SecondsPerBeat => 60.0 / bpm;
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public double SongTime { get; private set; }
        public double SongBeats => SongTime / SecondsPerBeat;
        public int CurrentBeat => (int)Math.Floor(SongBeats);
        public float BeatPhase => (float)(SongBeats - Math.Floor(SongBeats));

        public float Pulse
        {
            get
            {
                if (!IsRunning || SongBeats < 0.0)
                    return 0f;
                float p = Mathf.Clamp01(1f - BeatPhase / PulseBeats);
                return p * p;
            }
        }

        public static int Bounce(int pixels) => Instance != null ? Mathf.CeilToInt(Instance.Pulse * pixels - 0.001f) : 0;

        private double dspStart;
        private double lastDsp;
        private double realtimeAtLastDsp;
        private double pausedAtDsp;
        private bool pausedBeforeMusic;
        private int lastBeatFired;
        private bool audioChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnEnable() => AudioSettings.OnAudioConfigurationChanged += OnAudioChanged;

        private void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= OnAudioChanged;

        private void OnAudioChanged(bool deviceWasChanged) => audioChanged = true;

        public void StartSong(double leadBeats = 0.0)
        {
            if (music != null)
                music.Prepare();

            double lead = 0.15 + Math.Max(0.0, leadBeats) * SecondsPerBeat;
            dspStart = AudioSettings.dspTime + lead;
            if (music != null)
                music.PlayScheduled(dspStart);

            lastDsp = -1;
            lastBeatFired = -1;
            SongTime = -lead;
            IsRunning = true;
            IsPaused = false;
        }

        public void Pause()
        {
            if (!IsRunning || IsPaused)
                return;
            IsPaused = true;
            pausedAtDsp = AudioSettings.dspTime;
            pausedBeforeMusic = pausedAtDsp < dspStart;
            if (music != null)
                music.Pause();
        }

        public void Resume()
        {
            if (!IsRunning || !IsPaused)
                return;
            IsPaused = false;
            dspStart += AudioSettings.dspTime - pausedAtDsp;
            lastDsp = -1;
            if (music == null)
                return;
            if (pausedBeforeMusic)
                music.PlayScheduled(dspStart);
            else
                music.UnPause();
        }

        private void Resync(double raw)
        {
            audioChanged = false;
            dspStart += raw - SongTime;
            lastDsp = -1;
            if (music != null)
                music.Seek(dspStart);
            Sfx.Reschedule();
        }

        public double DspAtBeat(double beat) => dspStart + firstBeatSeconds + beat * SecondsPerBeat;

        public double OffsetToNearestBeat(double songTime, out int nearestBeat)
        {
            double beats = songTime / SecondsPerBeat;
            nearestBeat = (int)Math.Round(beats);
            return (beats - nearestBeat) * SecondsPerBeat;
        }

        private void Update()
        {
            if (!IsRunning || IsPaused)
                return;

            double dsp = AudioSettings.dspTime;
            double now = Time.realtimeSinceStartupAsDouble;
            if (dsp != lastDsp)
            {
                lastDsp = dsp;
                realtimeAtLastDsp = now;
            }
            double latency = outputLatencyMs / 1000.0;
            double raw = (lastDsp - dspStart) + Math.Min(now - realtimeAtLastDsp, MaxDriftSeconds) - firstBeatSeconds - latency;
            if (audioChanged || raw < SongTime - ResyncSeconds)
                Resync(raw);
            else
                SongTime = Math.Max(SongTime, raw);

            int beat = CurrentBeat;
            if (beat - lastBeatFired > 4)
                lastBeatFired = beat - 1;
            while (lastBeatFired < beat)
            {
                lastBeatFired++;
                if (lastBeatFired >= 0)
                    Beat?.Invoke(lastBeatFired);
            }
        }
    }
}
