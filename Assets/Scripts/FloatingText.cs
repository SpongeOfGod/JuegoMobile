using UnityEngine;

namespace Gluttony
{
    public class FloatingText : MonoBehaviour
    {
        private const float BlinkFrom = 0.7f;

        [SerializeField] private PixelLabel label;

        private RectTransform rect;
        private Vector2 origin;
        private float age;
        private float lifetime = 1f;
        private float rise;

        public void Show(string text, Vector2 canvasPosition, int scale, float seconds, float risePixelsPerSecond)
        {
            rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            origin = canvasPosition;
            rect.anchoredPosition = origin;
            label.Text = text;
            label.Scale = scale;
            lifetime = Mathf.Max(0.05f, seconds);
            rise = risePixelsPerSecond;
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            rect.anchoredPosition = origin + new Vector2(0f, Mathf.Floor(age * rise));
            float t = age / lifetime;
            label.enabled = t < BlinkFrom || Mathf.Repeat(age * 12f, 1f) < 0.5f;
            if (age >= lifetime)
                Destroy(gameObject);
        }
    }
}
