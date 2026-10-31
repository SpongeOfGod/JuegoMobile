using System;
using UnityEngine;

namespace Gluttony
{
    public enum MusicMood
    {
        Silent,
        Climb,
        Flight,
    }

    public enum MusicCue
    {
        Always,
        Moving,
    }

    [Serializable]
    public class MusicLayer
    {
        public string name;
        public AudioClip clip;
        public MusicCue cue;
        [Range(0f, 1f)] public float volume = 1f;
    }

    public class MusicPlayer : MonoBehaviour
    {
        private const float SilenceSeconds = 0.4f;

        [SerializeField] private MusicLayer[] layers = new MusicLayer[0];
        [SerializeField] private float volume = 0.8f;
        [SerializeField] private float fadeSeconds = 0.8f;

        private AudioSource[] sources;
        private float[] levels;
        private MusicMood mood = MusicMood.Silent;
        private float nearness;
        private float master = 1f;
        private float masterStep;

        public void SetSong(Song song)
        {
            if (layers.Length > 0)
            {
                layers[0].clip = song.clip;
                layers[0].volume = song.volume;
            }
            for (int i = 1; i < layers.Length; i++)
                layers[i].clip = song.dynamicLayer;
            if (sources == null)
                return;
            for (int i = 0; i < layers.Length; i++)
            {
                if (sources[i].clip == layers[i].clip)
                    continue;
                sources[i].Stop();
                sources[i].clip = layers[i].clip;
                if (sources[i].clip != null)
                    sources[i].clip.LoadAudioData();
            }
        }

        public void Prepare()
        {
            if (sources != null)
                return;
            sources = GetComponents<AudioSource>();
            levels = new float[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                sources[i].clip = layers[i].clip;
                if (sources[i].clip != null)
                    sources[i].clip.LoadAudioData();
            }
        }

        public void PlayScheduled(double dspTime)
        {
            foreach (var source in sources)
            {
                source.Stop();
                if (source.clip != null)
                    source.PlayScheduled(dspTime);
            }
        }

        public void Pause()
        {
            foreach (var source in sources)
                source.Pause();
        }

        public void UnPause()
        {
            foreach (var source in sources)
                source.UnPause();
        }

        public void Seek(double startDsp, float rate)
        {
            if (sources == null)
                return;
            double position = AudioSettings.dspTime - startDsp;
            foreach (var source in sources)
            {
                if (source.clip == null)
                    continue;
                source.Stop();
                if (position < 0.0)
                {
                    source.PlayScheduled(startDsp);
                    continue;
                }
                source.time = (float)(position * rate % source.clip.length);
                source.Play();
            }
        }

        public void FadeOut(float seconds) => masterStep = 1f / Mathf.Max(0.01f, seconds);

        public void SetPitch(float value)
        {
            if (sources == null)
                return;
            foreach (var source in sources)
                source.pitch = value;
        }

        public void SetNearness(float value) => nearness = Mathf.Clamp01(value);

        public void SetMood(MusicMood value, bool instant = false)
        {
            mood = value;
            if (!instant || sources == null)
                return;
            for (int i = 0; i < layers.Length; i++)
            {
                levels[i] = GetTargetLevel(layers[i]);
                ApplyLayerVolume(i);
            }
        }

        private float GetTargetLevel(MusicLayer layer)
        {
            if (mood == MusicMood.Silent)
                return 0f;
            if (layer.cue == MusicCue.Moving)
                return mood == MusicMood.Flight ? 1f : nearness;
            return 1f;
        }

        private void ApplyLayerVolume(int layerIndex)
        {
            sources[layerIndex].volume = levels[layerIndex] * layers[layerIndex].volume * volume * master;
        }

        private void Update()
        {
            if (sources == null)
                return;
            master = Mathf.MoveTowards(master, 0f, masterStep * Time.unscaledDeltaTime);
            float seconds = mood == MusicMood.Silent ? SilenceSeconds : fadeSeconds;
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
            for (int i = 0; i < layers.Length; i++)
            {
                levels[i] = Mathf.MoveTowards(levels[i], GetTargetLevel(layers[i]), step);
                ApplyLayerVolume(i);
            }
        }
    }
}
