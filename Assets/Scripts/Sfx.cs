using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gluttony
{
    public enum SfxId
    {
        Touch,
        Tick,
        Stab,
        Flip,
        Land,
        Launch,
        Skewer,
        Eat,
        Chew,
        Feast,
        Burp,
        Hurt,
        Explosion,
        Click,
        Rest,
        Miss,
        Whiff,
        Charge1,
        Charge2,
        Charge3,
        Release,
        Dash,
        Impact,
    }

    public struct SfxHandle
    {
        public int Voice;
        public double Start;
    }

    [Serializable]
    public class SfxClip
    {
        public SfxId id;
        public AudioClip clip;
        [Range(0f, 2f)] public float volume = 1f;
    }

    public class Sfx : MonoBehaviour
    {
        private const int VoiceCount = 24;
        private const float ClipFadeSeconds = 0.06f;
        private const double SchedulingLeadSeconds = 0.002;

        private static Sfx instance;
        private static readonly float[] Scale = { 1f, 1.1892f, 1.3348f, 1.4983f, 1.7818f, 2f, 2.3784f, 2.6697f, 2.9966f, 3.5636f };

        [SerializeField] private SfxClip[] clips = new SfxClip[0];
        [SerializeField] private float volume = 1f;
        [SerializeField] private float windVolume = 0.3f;
        [SerializeField] private AudioClip windClip;

        private sealed class SoundVoice
        {
            public AudioSource Source;
            public double BusyUntil;
            public double StartTime;
            public ScheduledSound Sound;
            public bool IsPaused;

            public void Stop()
            {
                Source.Stop();
                BusyUntil = 0.0;
                StartTime = 0.0;
            }
        }

        private struct ScheduledSound
        {
            public SfxId Id;
            public double Beat;
            public float Pitch;
            public float Volume;
        }

        private SoundVoice[] voices;
        private AudioClip[] preparedClips;
        private float[] clipVolumes;
        private readonly List<ScheduledSound> pausedSounds = new List<ScheduledSound>();
        private AudioSource windSource;
        private float windLevel;

        public static float Note(int index)
        {
            return Scale[Mathf.Abs(index) % Scale.Length];
        }

        private void Awake()
        {
            instance = this;
            CreateVoices();
            PrepareClips();
            CreateWindSource();
        }

        private void CreateVoices()
        {
            voices = new SoundVoice[VoiceCount];
            for (int i = 0; i < voices.Length; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                voices[i] = new SoundVoice { Source = source };
            }
        }

        private void PrepareClips()
        {
            int soundCount = Enum.GetValues(typeof(SfxId)).Length;
            preparedClips = new AudioClip[soundCount];
            clipVolumes = new float[soundCount];

            foreach (SfxClip entry in clips)
            {
                if (entry == null || entry.clip == null)
                    continue;

                int soundIndex = (int)entry.id;
                if (entry.id == SfxId.Stab)
                    preparedClips[soundIndex] = entry.clip;
                else
                    preparedClips[soundIndex] = AudioClipProcessing.SmoothEnding(entry.clip, ClipFadeSeconds);

                clipVolumes[soundIndex] = entry.volume;
            }

            for (int i = 0; i < preparedClips.Length; i++)
            {
                bool isSkewer = (SfxId)i == SfxId.Skewer;
                float targetRms = isSkewer ? 0.2f : 0.126f;
                float peakCeiling = isSkewer ? 0.89f : 0.708f;
                preparedClips[i] = AudioClipProcessing.NormalizeVolume(preparedClips[i], targetRms, peakCeiling);
            }
        }

        private void CreateWindSource()
        {
            windSource = gameObject.AddComponent<AudioSource>();
            windSource.playOnAwake = false;
            windSource.loop = true;
            windSource.clip = windClip;
            windSource.volume = 0f;
            if (windClip != null)
                windSource.Play();
        }

        public static void Wind(float level)
        {
            if (instance != null)
                instance.windLevel = Mathf.Clamp01(level);
        }

        private void Update()
        {
            float targetVolume = Time.timeScale > 0f ? windLevel * windVolume * volume : 0f;
            windSource.volume = Mathf.MoveTowards(windSource.volume, targetVolume, Time.unscaledDeltaTime * 2.5f);
        }

        public static void Play(SfxId id, float pitch = 1f, float volume = 1f)
        {
            if (instance != null)
                instance.PlayVoice(id, 0.0, double.NaN, pitch, volume);
        }

        public static SfxHandle PlayAtBeat(SfxId id, double beat, float pitch = 1f, float volume = 1f)
        {
            if (instance == null)
                return new SfxHandle { Voice = -1 };

            Conductor conductor = Conductor.Instance;
            double startTime = 0.0;
            if (conductor != null && conductor.IsRunning && !conductor.IsPaused)
                startTime = conductor.DspAtBeat(beat);

            return instance.PlayVoice(id, startTime, beat, pitch, volume);
        }

        public static void Cancel(SfxHandle handle)
        {
            if (instance == null || handle.Voice < 0 || handle.Voice >= instance.voices.Length)
                return;

            SoundVoice voice = instance.voices[handle.Voice];
            if (voice.StartTime != handle.Start || handle.Start <= AudioSettings.dspTime)
                return;

            voice.Stop();
        }

        public static void PauseAll()
        {
            if (instance == null)
                return;

            double currentTime = AudioSettings.dspTime;
            foreach (SoundVoice voice in instance.voices)
            {
                if (voice.StartTime > currentTime)
                {
                    if (!double.IsNaN(voice.Sound.Beat))
                        instance.pausedSounds.Add(voice.Sound);
                    voice.Stop();
                }
                else if (voice.Source.isPlaying)
                {
                    voice.Source.Pause();
                    voice.IsPaused = true;
                }
            }
        }

        public static void ResumeAll()
        {
            if (instance == null)
                return;

            foreach (SoundVoice voice in instance.voices)
            {
                if (!voice.IsPaused)
                    continue;
                voice.IsPaused = false;
                voice.Source.UnPause();
            }

            ScheduledSound[] soundsToResume = instance.pausedSounds.ToArray();
            instance.pausedSounds.Clear();
            foreach (ScheduledSound sound in soundsToResume)
                PlayAtBeat(sound.Id, sound.Beat, sound.Pitch, sound.Volume);
        }

        public static void Reschedule()
        {
            if (instance == null || Conductor.Instance == null)
                return;

            double currentBeat = Conductor.Instance.SongBeats;
            foreach (SoundVoice voice in instance.voices)
            {
                ScheduledSound sound = voice.Sound;
                if (double.IsNaN(sound.Beat) || sound.Beat <= currentBeat || voice.IsPaused)
                    continue;
                voice.Stop();
                PlayAtBeat(sound.Id, sound.Beat, sound.Pitch, sound.Volume);
            }
        }

        public static void CancelScheduled()
        {
            if (instance == null)
                return;

            instance.pausedSounds.Clear();
            foreach (SoundVoice voice in instance.voices)
            {
                if (!voice.IsPaused)
                    continue;
                voice.IsPaused = false;
                voice.Source.Stop();
            }

            double currentTime = AudioSettings.dspTime;
            foreach (SoundVoice voice in instance.voices)
            {
                if (voice.StartTime > currentTime)
                    voice.Stop();
            }
        }

        private int FindAvailableVoice()
        {
            int selectedIndex = 0;
            for (int i = 1; i < voices.Length; i++)
            {
                if (voices[i].BusyUntil < voices[selectedIndex].BusyUntil)
                    selectedIndex = i;
            }
            return selectedIndex;
        }

        private SfxHandle PlayVoice(SfxId id, double scheduledTime, double beat, float pitch, float soundVolume)
        {
            AudioClip clip = preparedClips[(int)id];
            if (clip == null)
                return new SfxHandle { Voice = -1 };

            double currentTime = AudioSettings.dspTime;
            int voiceIndex = FindAvailableVoice();
            SoundVoice voice = voices[voiceIndex];
            AudioSource source = voice.Source;
            source.Stop();
            voice.IsPaused = false;
            voice.Sound = new ScheduledSound { Id = id, Beat = beat, Pitch = pitch, Volume = soundVolume };
            source.clip = clip;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(soundVolume * clipVolumes[(int)id] * volume);

            bool playLater = scheduledTime > currentTime + SchedulingLeadSeconds;
            double startTime = playLater ? scheduledTime : currentTime;
            if (playLater)
                source.PlayScheduled(scheduledTime);
            else
                source.Play();

            voice.StartTime = startTime;
            voice.BusyUntil = startTime + clip.length / Mathf.Max(0.1f, pitch);
            return new SfxHandle { Voice = voiceIndex, Start = startTime };
        }
    }
}
