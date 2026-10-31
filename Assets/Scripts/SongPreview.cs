using UnityEngine;

namespace Gluttony
{
    public class SongPreview : MonoBehaviour
    {
        private const float FadeSeconds = 0.3f;

        [SerializeField] private float volume = 0.4f;

        private AudioSource source;
        private Song song;
        private float level;
        private float stopAt;
        private bool stopping;

        public bool IsPlaying => source != null && source.isPlaying && !stopping;
        public float Remaining => IsPlaying ? Mathf.Max(0f, stopAt - Time.unscaledTime) : 0f;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
        }

        public void Play(Song value, float seconds)
        {
            bool sameSong = song == value && source.clip == value.clip && source.isPlaying;
            song = value;
            stopAt = Time.unscaledTime + seconds;
            stopping = false;
            if (sameSong || value.clip == null)
                return;
            source.Stop();
            if (source.clip != null)
                source.clip.UnloadAudioData();
            source.clip = value.clip;
            value.clip.LoadAudioData();
            source.time = Mathf.Min(value.previewSeconds, value.clip.length - 0.1f);
            level = 0f;
            ApplyVolume();
            source.Play();
        }

        public void Stop() => stopping = true;

        public void StopNow()
        {
            stopping = true;
            level = 0f;
            source.Stop();
        }

        private void ApplyVolume()
        {
            if (song != null)
                source.volume = level * song.volume * volume;
        }

        private void OnDestroy()
        {
            MenuMusic.Duck(false);
            if (source.clip != null)
                source.clip.UnloadAudioData();
        }

        private void Update()
        {
            MenuMusic.Duck(source.isPlaying);
            if (!source.isPlaying)
                return;
            if (Time.unscaledTime >= stopAt)
                stopping = true;
            level = Mathf.MoveTowards(level, stopping ? 0f : 1f, Time.unscaledDeltaTime / FadeSeconds);
            ApplyVolume();
            if (stopping && level <= 0f)
                source.Stop();
        }
    }
}
