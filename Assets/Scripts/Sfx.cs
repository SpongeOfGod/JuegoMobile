using System;
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
        private const int Voices = 24;
        private const int KeySemitones = 5;
        private const float ClipFadeSeconds = 0.06f;

        private static Sfx instance;

        private static readonly float[] Scale = { 1f, 1.1892f, 1.3348f, 1.4983f, 1.7818f, 2f, 2.3784f, 2.6697f, 2.9966f, 3.5636f };

        [SerializeField] private SfxClip[] clips = new SfxClip[0];

        [SerializeField] private float volume = 1f;
        [SerializeField] private float windVolume = 0.3f;
        [SerializeField] private AudioClip windClip;

        private AudioSource[] pool;
        private double[] busyUntil;
        private double[] startsAt;
        private AudioClip[] resolved;
        private float[] gains;
        private float[] transpose;
        private SfxId[] voiceIds;
        private double[] voiceBeats;
        private float[] voicePitches;
        private float[] voiceVolumes;
        private bool[] voicePaused;

        private readonly System.Collections.Generic.List<(SfxId Id, double Beat, float Pitch, float Volume)> held = new System.Collections.Generic.List<(SfxId, double, float, float)>();
        private AudioSource wind;
        private float windLevel;

        public static float Note(int index) => Scale[Mathf.Abs(index) % Scale.Length];

        private void Awake()
        {
            instance = this;
            pool = new AudioSource[Voices];
            busyUntil = new double[Voices];
            startsAt = new double[Voices];
            voiceIds = new SfxId[Voices];
            voiceBeats = new double[Voices];
            voicePitches = new float[Voices];
            voiceVolumes = new float[Voices];
            voicePaused = new bool[Voices];
            for (int i = 0; i < Voices; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }

            int count = Enum.GetValues(typeof(SfxId)).Length;
            resolved = new AudioClip[count];
            gains = new float[count];
            transpose = new float[count];
            for (int i = 0; i < count; i++)
            {
                resolved[i] = Synthesize((SfxId)i);
                gains[i] = 1f;
                transpose[i] = Mathf.Pow(2f, KeySemitones / 12f);
            }
            foreach (var entry in clips)
            {
                if (entry == null || entry.clip == null)
                    continue;
                resolved[(int)entry.id] = Smoothed(entry.clip, ClipFadeSeconds);
                transpose[(int)entry.id] = 1f;
                gains[(int)entry.id] = entry.volume;
            }

            wind = gameObject.AddComponent<AudioSource>();
            wind.playOnAwake = false;
            wind.loop = true;
            wind.clip = windClip != null ? windClip : WindLoop();
            wind.volume = 0f;
            wind.Play();
        }

        public static void Wind(float level)
        {
            if (instance != null)
                instance.windLevel = Mathf.Clamp01(level);
        }

        private void Update()
        {
            float target = Time.timeScale > 0f ? windLevel * windVolume * volume : 0f;
            wind.volume = Mathf.MoveTowards(wind.volume, target, Time.unscaledDeltaTime * 2.5f);
        }

        public static void Play(SfxId id, float pitch = 1f, float volume = 1f)
        {
            if (instance != null)
                instance.Voice(id, 0.0, double.NaN, pitch, volume);
        }

        public static SfxHandle PlayAtBeat(SfxId id, double beat, float pitch = 1f, float volume = 1f)
        {
            if (instance == null)
                return new SfxHandle { Voice = -1 };
            var conductor = Conductor.Instance;
            double dsp = conductor != null && conductor.IsRunning && !conductor.IsPaused ? conductor.DspAtBeat(beat) : 0.0;
            return instance.Voice(id, dsp, beat, pitch, volume);
        }

        public static void Cancel(SfxHandle handle)
        {
            if (instance == null || handle.Voice < 0 || handle.Voice >= instance.pool.Length)
                return;
            int i = handle.Voice;
            if (instance.startsAt[i] != handle.Start || handle.Start <= AudioSettings.dspTime)
                return;
            instance.pool[i].Stop();
            instance.busyUntil[i] = 0.0;
            instance.startsAt[i] = 0.0;
        }

        public static void PauseAll()
        {
            if (instance == null)
                return;
            double now = AudioSettings.dspTime;
            for (int i = 0; i < instance.pool.Length; i++)
            {
                if (instance.startsAt[i] > now)
                {
                    if (!double.IsNaN(instance.voiceBeats[i]))
                        instance.held.Add((instance.voiceIds[i], instance.voiceBeats[i], instance.voicePitches[i], instance.voiceVolumes[i]));
                    instance.pool[i].Stop();
                    instance.busyUntil[i] = 0.0;
                    instance.startsAt[i] = 0.0;
                }
                else if (instance.pool[i].isPlaying)
                {
                    instance.pool[i].Pause();
                    instance.voicePaused[i] = true;
                }
            }
        }

        public static void ResumeAll()
        {
            if (instance == null)
                return;
            for (int i = 0; i < instance.pool.Length; i++)
            {
                if (!instance.voicePaused[i])
                    continue;
                instance.voicePaused[i] = false;
                instance.pool[i].UnPause();
            }
            var again = instance.held.ToArray();
            instance.held.Clear();
            foreach (var sound in again)
                PlayAtBeat(sound.Id, sound.Beat, sound.Pitch, sound.Volume);
        }

        public static void CancelScheduled()
        {
            if (instance == null)
                return;
            instance.held.Clear();
            for (int i = 0; i < instance.pool.Length; i++)
            {
                if (!instance.voicePaused[i])
                    continue;
                instance.voicePaused[i] = false;
                instance.pool[i].Stop();
            }
            double now = AudioSettings.dspTime;
            for (int i = 0; i < instance.pool.Length; i++)
            {
                if (instance.startsAt[i] > now)
                {
                    instance.pool[i].Stop();
                    instance.busyUntil[i] = 0.0;
                    instance.startsAt[i] = 0.0;
                }
            }
        }

        private SfxHandle Voice(SfxId id, double dsp, double beat, float pitch, float volume)
        {
            double now = AudioSettings.dspTime;
            int pick = 0;
            for (int i = 1; i < pool.Length; i++)
                if (busyUntil[i] < busyUntil[pick])
                    pick = i;

            var clip = resolved[(int)id];
            var source = pool[pick];
            source.Stop();
            voicePaused[pick] = false;
            voiceIds[pick] = id;
            voiceBeats[pick] = beat;
            voicePitches[pick] = pitch;
            voiceVolumes[pick] = volume;
            source.clip = clip;
            pitch *= transpose[(int)id];
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(volume * gains[(int)id] * this.volume);
            double start = dsp > now + 0.002 ? dsp : now;
            if (dsp > now + 0.002)
                source.PlayScheduled(dsp);
            else
                source.Play();
            startsAt[pick] = start;
            busyUntil[pick] = start + clip.length / Mathf.Max(0.1f, pitch);
            return new SfxHandle { Voice = pick, Start = start };
        }

        private static AudioClip Smoothed(AudioClip clip, float fadeSeconds)
        {
            if (clip.loadState != AudioDataLoadState.Loaded)
                clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded)
                return clip;
            int channels = clip.channels;
            int frames = clip.samples;
            int rate = clip.frequency;
            var source = new float[frames * channels];
            if (!clip.GetData(source, 0))
                return clip;

            int window = Mathf.Max(1, rate / 50);
            int edge = Mathf.Max(1, rate / 500);
            float loudest = 0.0001f;
            for (int start = 0; start + window <= frames; start += window)
                loudest = Mathf.Max(loudest, Rms(source, channels, start, window));
            int last = Mathf.Min(window, frames);
            float ending = Rms(source, channels, frames - last, last);
            bool abrupt = frames > edge && Rms(source, channels, frames - edge, edge) > ending * 0.6f;
            bool truncated = abrupt && frames > window * 4 && ending / loudest > 0.05f;

            int grain = rate / 25;
            int hop = grain / 2;
            int tail = truncated ? Mathf.RoundToInt(rate * 0.35f) : 0;
            int total = frames + tail;
            var data = new float[total * channels];
            System.Array.Copy(source, data, source.Length);

            if (truncated)
            {
                var random = new System.Random(frames);
                int pickFrom = Mathf.Max(0, frames - Mathf.RoundToInt(rate * 0.08f) - grain);
                int pickTo = frames - grain;
                for (int frame = frames - hop; frame < frames; frame++)
                {
                    float w = 0.5f + 0.5f * Mathf.Cos(Mathf.PI * (frame - (frames - hop)) / hop);
                    for (int c = 0; c < channels; c++)
                        data[frame * channels + c] *= w;
                }
                for (int at = frames - hop, k = 0; at < total; at += hop, k++)
                {
                    int from = pickFrom + random.Next(Mathf.Max(1, pickTo - pickFrom));
                    float gain = Mathf.Exp(-k * hop / (rate * 0.09f));
                    for (int n = 0; n < grain && at + n < total; n++)
                    {
                        if (at + n < frames - hop)
                            continue;
                        float w = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * n / grain);
                        for (int c = 0; c < channels; c++)
                            data[(at + n) * channels + c] += source[(from + n) * channels + c] * w * gain;
                    }
                }
            }

            int fadeIn = Mathf.Max(1, Mathf.Min(total / 4, rate / 1000));
            int fadeOut = abrupt ? Mathf.Max(1, Mathf.Min(total / 3, Mathf.RoundToInt(rate * fadeSeconds))) : 0;
            for (int frame = 0; frame < total; frame++)
            {
                float gain = 1f;
                if (frame < fadeIn)
                    gain = frame / (float)fadeIn;
                int left = total - 1 - frame;
                if (left < fadeOut)
                    gain *= 0.5f - 0.5f * Mathf.Cos(Mathf.PI * left / fadeOut);
                for (int c = 0; c < channels; c++)
                    data[frame * channels + c] *= gain;
            }
            var copy = AudioClip.Create(clip.name, total, channels, rate, false);
            copy.SetData(data, 0);
            return copy;
        }

        private static float Rms(float[] data, int channels, int start, int length)
        {
            double sum = 0;
            for (int frame = start; frame < start + length; frame++)
                for (int c = 0; c < channels; c++)
                {
                    float v = data[frame * channels + c];
                    sum += v * v;
                }
            return (float)Math.Sqrt(sum / (length * channels));
        }

        private static AudioClip Synthesize(SfxId id)
        {
            switch (id)
            {
                case SfxId.Touch: return Tone("sfx_touch", 0.04f, 900f, 700f, 0.16f, Wave.Square, 80f);
                case SfxId.Tick: return Tone("sfx_tick", 0.07f, 880f, 880f, 0.2f, Wave.Square, 45f);
                case SfxId.Stab: return Tone("sfx_stab", 0.14f, 2600f, 1900f, 0.3f, Wave.Square, 28f, 0.2f, 0.015f);
                case SfxId.Flip: return Tone("sfx_flip", 0.16f, 200f, 200f, 0.16f, Wave.Noise, 12f);
                case SfxId.Land: return Tone("sfx_land", 0.09f, 140f, 60f, 0.55f, Wave.Sine, 40f, 0.3f);
                case SfxId.Launch: return Tone("sfx_launch", 0.45f, 180f, 1200f, 0.4f, Wave.Square, 4f, 0.15f);
                case SfxId.Skewer: return SkewerHit();
                case SfxId.Eat: return Tone("sfx_eat", 0.14f, 330f, 165f, 0.5f, Wave.Square, 22f, 0.3f, 0.02f);
                case SfxId.Feast: return Tone("sfx_feast", 0.5f, 523f, 1046f, 0.35f, Wave.Square, 5f);
                case SfxId.Hurt: return Tone("sfx_hurt", 0.3f, 300f, 70f, 0.5f, Wave.Saw, 8f, 0.2f);
                case SfxId.Explosion: return Tone("sfx_explosion", 0.4f, 120f, 40f, 0.6f, Wave.Noise, 7f);
                case SfxId.Click: return Tone("sfx_click", 0.05f, 1000f, 800f, 0.2f, Wave.Square, 60f);
                case SfxId.Rest: return Tone("sfx_rest", 0.5f, 440f, 880f, 0.3f, Wave.Sine, 4f);
                case SfxId.Miss: return Tone("sfx_miss", 0.2f, 220f, 110f, 0.35f, Wave.Square, 10f);
                case SfxId.Whiff: return Tone("sfx_whiff", 0.24f, 700f, 140f, 0.3f, Wave.Square, 9f, 0.55f);
                case SfxId.Chew: return Tone("sfx_chew", 0.06f, 170f, 120f, 0.35f, Wave.Noise, 45f);
                case SfxId.Burp: return Tone("sfx_burp", 0.4f, 95f, 55f, 0.55f, Wave.Saw, 5f, 0.35f);
                case SfxId.Charge1: return Tone("sfx_charge1", 0.07f, 440f, 440f, 0.2f, Wave.Square, 45f);
                case SfxId.Charge2: return Tone("sfx_charge2", 0.07f, 523f, 523f, 0.2f, Wave.Square, 45f);
                case SfxId.Charge3: return Tone("sfx_charge3", 0.07f, 659f, 659f, 0.2f, Wave.Square, 45f);
                case SfxId.Release: return Tone("sfx_release", 0.14f, 2600f, 1900f, 0.3f, Wave.Square, 28f, 0.2f, 0.015f);
                case SfxId.Dash: return Tone("sfx_dash", 0.07f, 900f, 300f, 0.18f, Wave.Noise, 30f);
                case SfxId.Impact: return Tone("sfx_impact", 0.6f, 90f, 40f, 0.5f, Wave.Noise, 6f);
                default: return Tone("sfx", 0.05f, 440f, 440f, 0.2f, Wave.Sine, 30f);
            }
        }

        private enum Wave { Sine, Square, Saw, Noise }

        private static AudioClip SkewerHit()
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
            int length = (int)(0.24f * rate);
            var data = new float[length];
            var random = new System.Random(11);
            double phase = 0;
            float low = 0f;
            float band = 0f;
            float peak = 0.0001f;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);

                float freq = 150f + 750f * Mathf.Exp(-t / 0.012f);
                phase += freq / rate;
                float thunk = Mathf.Sin((float)(phase * 2.0 * Math.PI)) * Mathf.Exp(-t / 0.07f);

                float center = Mathf.Lerp(2400f, 450f, Mathf.Clamp01(t / 0.13f));
                float k = 2f * Mathf.Sin(Mathf.PI * center / rate);
                low += k * band;
                float high = noise - low - 0.35f * band;
                band += k * high;
                float gurgle = 0.6f + 0.4f * Mathf.Sin(t * Mathf.PI * 2f * 38f);
                float squelch = band * Mathf.Exp(-t / 0.055f) * gurgle;

                float click = t < 0.004f ? noise * (1f - t / 0.004f) : 0f;

                float s = thunk * 0.9f + squelch * 0.55f + click * 0.5f;
                s = (float)Math.Tanh(s * 1.8f);
                float edge = Mathf.Min(1f, t * 2000f) * Mathf.Min(1f, (length - i) / (rate * 0.01f));
                data[i] = s * edge;
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            for (int i = 0; i < length; i++)
                data[i] = data[i] / peak * 0.6f;
            var clip = AudioClip.Create("sfx_skewer", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip WindLoop()
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
            int length = rate * 2;
            int fade = rate / 4;
            var raw = new float[length + fade];
            var random = new System.Random(7);
            float low = 0f;
            float band = 0f;
            float peak = 0.0001f;
            for (int i = 0; i < raw.Length; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (white - low) * 0.08f;
                band += (low - band) * 0.02f;
                float t = (float)i / rate;
                float gust = 0.75f + 0.25f * Mathf.Sin(t * Mathf.PI * 2f * 0.5f);
                raw[i] = (low - band) * gust;
                peak = Mathf.Max(peak, Mathf.Abs(raw[i]));
            }
            var data = new float[length];
            for (int i = 0; i < length; i++)
                data[i] = raw[i] / peak;
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                data[i] = (raw[i] * k + raw[length + i] * (1f - k)) / peak;
            }
            var clip = AudioClip.Create("sfx_wind", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Tone(string name, float seconds, float fromHz, float toHz, float gain, Wave wave, float decay, float noiseMix = 0f, float noiseAttack = 0f)
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
            int length = Mathf.Max(1, (int)(seconds * rate));
            var data = new float[length];
            var random = new System.Random(name.GetHashCode());
            double phase = 0;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate;
                float k = t / seconds;
                float freq = Mathf.Lerp(fromHz, toHz, k);
                phase += freq / rate;
                float frac = (float)(phase - System.Math.Floor(phase));
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float s;
                switch (wave)
                {
                    case Wave.Square: s = frac < 0.5f ? 1f : -1f; break;
                    case Wave.Saw: s = frac * 2f - 1f; break;
                    case Wave.Noise: s = noise; break;
                    default: s = Mathf.Sin(frac * 2f * Mathf.PI); break;
                }
                float mix = t < noiseAttack ? 1f : noiseMix;
                s = Mathf.Lerp(s, noise, mix);
                float env = Mathf.Min(1f, t * 800f) * Mathf.Exp(-t * decay) * Mathf.Min(1f, (length - i) / (rate * 0.005f));
                data[i] = s * env * gain;
            }
            var clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
