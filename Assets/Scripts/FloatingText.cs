using UnityEngine;

namespace Gluttony
{
    public class FloatingText : MonoBehaviour
    {
        private const float Lifetime = 1f;
        private const float RisePixelsPerSecond = 24f;
        private const float BlinkFrom = 0.7f;

        [SerializeField] private PixelLabel label;

        private RectTransform rect;
        private Vector2 origin;
        private float age;

        public void Show(string text, Color tint, Vector2 canvasPosition, int scale = 1)
        {
            rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            origin = canvasPosition;
            rect.anchoredPosition = origin;
            label.Text = text;
            label.color = tint;
            label.Scale = scale;
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            rect.anchoredPosition = origin + new Vector2(0f, Mathf.Floor(age * RisePixelsPerSecond));
            float t = age / Lifetime;
            label.enabled = t < BlinkFrom || Mathf.Repeat(age * 12f, 1f) < 0.5f;
            if (age >= Lifetime)
                Destroy(gameObject);
        }
    }
}
