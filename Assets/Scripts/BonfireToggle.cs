using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gluttony
{
    [RequireComponent(typeof(Image), typeof(SpriteFlipbook), typeof(AudioSource))]
    public class BonfireToggle : MonoBehaviour, IPointerClickHandler
    {
        private const int TapsToPutOut = 20;

        private static int taps;

        [SerializeField] private Sprite outSprite;
        [SerializeField] private SpriteFlipbook cat;
        [SerializeField] private Sprite[] darkCatFrames;

        private Image image;
        private SpriteFlipbook flames;
        private AudioSource jingle;
        private bool lit = true;
        private bool ducking;
        private Sprite[] litCatFrames;

        private void Awake()
        {
            image = GetComponent<Image>();
            flames = GetComponent<SpriteFlipbook>();
            jingle = GetComponent<AudioSource>();
            litCatFrames = cat.Frames;
            if (SongStore.CopyrightUnlocked)
                PutOut();
            else
                jingle.clip.LoadAudioData();
        }

        private void Update()
        {
            if (ducking && !jingle.isPlaying)
                Unduck();
        }

        private void OnDestroy()
        {
            if (ducking)
                Unduck();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (SongStore.CopyrightUnlocked)
                return;
            UiSounds.Play(UiSound.Click);
            taps++;
            if (taps >= TapsToPutOut)
            {
                SongStore.CopyrightUnlocked = true;
                PutOut();
                MenuMusic.Duck(true);
                ducking = true;
                jingle.Play();
                return;
            }
            lit = !lit;
            flames.enabled = lit;
            cat.Frames = lit ? litCatFrames : darkCatFrames;
            if (!lit)
                image.sprite = outSprite;
        }

        private void PutOut()
        {
            flames.enabled = false;
            cat.Frames = darkCatFrames;
            image.sprite = outSprite;
            image.raycastTarget = false;
        }

        private void Unduck()
        {
            ducking = false;
            MenuMusic.Duck(false);
        }
    }
}
