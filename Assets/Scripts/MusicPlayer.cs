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

        public void Prepare()
        {
            if (sources != null)
                return;
            sources = new AudioSource[layers.Length];
            levels = new float[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.clip = layers[i].clip;
                source.volume = 0f;
                if (source.clip != null)
                    source.clip.LoadAudioData();
                sources[i] = source;
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

        public void SetNearness(float value) => nearness = Mathf.Clamp01(value);

        public void SetMood(MusicMood value, bool instant = false)
        {
            mood = value;
            if (!instant || sources == null)
                return;
            for (int i = 0; i < layers.Length; i++)
            {
                levels[i] = Wanted(layers[i]);
                Apply(i);
            }
        }

        private float Wanted(MusicLayer layer)
        {
            if (mood == MusicMood.Silent)
                return 0f;
            if (layer.cue == MusicCue.Moving)
                return mood == MusicMood.Flight ? 1f : nearness;
            return 1f;
        }

        private void Apply(int i) => sources[i].volume = levels[i] * layers[i].volume * volume;

        private void Update()
        {
            if (sources == null)
                return;
            float seconds = mood == MusicMood.Silent ? SilenceSeconds : fadeSeconds;
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
            for (int i = 0; i < layers.Length; i++)
            {
                levels[i] = Mathf.MoveTowards(levels[i], Wanted(layers[i]), step);
                Apply(i);
            }
        }
    }
}
