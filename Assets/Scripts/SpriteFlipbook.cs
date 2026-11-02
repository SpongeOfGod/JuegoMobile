using UnityEngine;
using UnityEngine.UI;

namespace Gluttony
{
    [RequireComponent(typeof(Image))]
    public class SpriteFlipbook : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames = new Sprite[0];
        [SerializeField] private float framesPerSecond = 5f;

        private Image image;

        public Sprite[] Frames
        {
            get => frames;
            set => frames = value;
        }

        private void Awake() => image = GetComponent<Image>();

        private void Update()
        {
            if (frames.Length > 0)
                image.sprite = frames[(int)(Time.unscaledTime * framesPerSecond) % frames.Length];
        }
    }
}
