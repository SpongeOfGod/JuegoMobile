using UnityEngine;
using UnityEngine.UI;

namespace Gluttony
{
    public class ChargeMeter : MonoBehaviour
    {
        private const float ShowSeconds = 0.1f;
        private const float HideSeconds = 0.16f;
        private const int SlidePixels = 4;
        private const float KickSeconds = 0.1f;
        private const int KickPixels = 2;
        private const int AlphaSteps = 4;

        [SerializeField] private GameObject bar;
        [SerializeField] private Image frame;
        [SerializeField] private Image[] segments = new Image[0];

        private Image[] images;
        private Color[] colors;
        private RectTransform barRect;
        private float home;
        private float visible;
        private int lastLit;
        private float kickAt = float.NegativeInfinity;
        private bool cut;
        private CatController cat;

        private void Awake()
        {
            barRect = (RectTransform)bar.transform;
            home = barRect.anchoredPosition.y;
            images = bar.GetComponentsInChildren<Image>(true);
            colors = new Color[images.Length];
            for (int i = 0; i < images.Length; i++)
                colors[i] = images[i].color;
        }

        private void Start()
        {
            cat = CatController.Instance;
            if (cat != null)
                cat.Jumped += OnJumped;
        }

        private void OnDestroy()
        {
            if (cat != null)
                cat.Jumped -= OnJumped;
        }

        private void OnJumped()
        {
            cut = true;
            visible = 0f;
            lastLit = 0;
            bar.SetActive(false);
        }

        private void LateUpdate()
        {
            bool shown = cat != null && cat.ChargeShown && !GameSettings.LegacyChargeBar;
            if (!shown)
                cut = false;
            bool show = shown && !cut;
            if (cut)
                return;
            visible = Mathf.MoveTowards(visible, show ? 1f : 0f, Time.unscaledDeltaTime / (show ? ShowSeconds : HideSeconds));
            bool active = visible > 0f;
            if (bar.activeSelf != active)
                bar.SetActive(active);
            if (!active)
            {
                lastLit = 0;
                return;
            }

            if (show)
            {
                if (cat.ChargeLit > lastLit)
                    kickAt = Time.unscaledTime;
                lastLit = cat.ChargeLit;
                for (int i = 0; i < segments.Length; i++)
                {
                    segments[i].enabled = i < cat.ChargeLit;
                    colors[System.Array.IndexOf(images, segments[i])] = cat.ChargeSegmentWhite(i) ? Palette.White : Palette.Red;
                }
                colors[System.Array.IndexOf(images, frame)] = cat.ChargeFrameRed ? Palette.Red : Palette.White;
            }

            float eased = visible * visible * (3f - 2f * visible);
            float alpha = Mathf.Ceil(eased * AlphaSteps) / AlphaSteps;
            for (int i = 0; i < images.Length; i++)
            {
                Color color = colors[i];
                color.a *= alpha;
                images[i].color = color;
            }

            int slide = -Mathf.RoundToInt((1f - eased) * SlidePixels);
            int kick = Time.unscaledTime - kickAt < KickSeconds ? KickPixels : 0;
            int bounce = show ? Conductor.Bounce(1) : 0;
            int jitter = show && cat.ChargeFrameRed ? (Mathf.FloorToInt(Time.unscaledTime * 16f) % 2 == 0 ? 1 : -1) : 0;
            barRect.anchoredPosition = new Vector2(jitter, home + slide + kick - bounce);
        }
    }
}
