using UnityEngine;
using UnityEngine.UI;

namespace Gluttony
{
    public enum UiSound
    {
        Click,
        Approved,
        Declined,
    }

    public class UiSounds : MonoBehaviour
    {
        private static UiSounds instance;

        [SerializeField] private AudioClip click;
        [SerializeField] private AudioClip approved;
        [SerializeField] private AudioClip declined;
        [SerializeField] private float volume = 0.7f;

        private AudioSource source;

        private void Awake()
        {
            instance = this;
            source = GetComponent<AudioSource>();
            foreach (var button in FindObjectsOfType<Button>(true))
                Hook(button);
        }

        public static void Hook(Button button) => button.onClick.AddListener(() => Play(UiSound.Click));

        public static void Play(UiSound sound)
        {
            if (instance == null)
                return;
            AudioClip clip = sound == UiSound.Click ? instance.click : sound == UiSound.Approved ? instance.approved : instance.declined;
            if (clip != null)
                instance.source.PlayOneShot(clip, instance.volume);
        }
    }
}
