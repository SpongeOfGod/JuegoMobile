using UnityEngine;

namespace Gluttony
{
    public class MenuMusic : MonoBehaviour
    {
        private const float Bpm = 130f;
        private const float FirstBeatSeconds = 0.2308f;
        private const float DuckSeconds = 0.3f;

        private static MenuMusic instance;
        private static bool ducked;

        [SerializeField] private float volume = 0.5f;

        private AudioSource source;
        private float level = 1f;

        public static float Pulse
        {
            get
            {
                if (instance == null || !instance.source.isPlaying)
                    return 0f;
                float seconds = instance.source.timeSamples / (float)instance.source.clip.frequency - FirstBeatSeconds;
                float phase = Mathf.Repeat(seconds * Bpm / 60f, 1f);
                return (1f - phase) * (1f - phase) * instance.level;
            }
        }

        public static void Duck(bool value) => ducked = value;

        public static void Release()
        {
            if (instance != null)
                Destroy(instance.gameObject);
            instance = null;
        }

        private void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            ducked = false;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            source = GetComponent<AudioSource>();
            source.volume = volume;
            source.Play();
        }

        private void Update()
        {
            level = Mathf.MoveTowards(level, ducked ? 0f : 1f, Time.unscaledDeltaTime / DuckSeconds);
            source.volume = volume * level;
        }
    }
}
