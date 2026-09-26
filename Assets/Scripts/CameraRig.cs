using UnityEngine;

namespace Gluttony
{
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        private const float CatScreenFraction = 0.35f;
        private const float LaunchScreenFraction = 0.16f;
        private const float StartScreenFraction = 0.2f;
        private const float SmoothTime = 0.15f;
        private const float BlastHoldBeats = 0.5f;

        [SerializeField] private float[] creepPerBeat = { 0.11f, 0.17f, 0.22f, 0.28f, 0.34f };

        public Camera Camera { get; private set; }
        public float HalfHeight => Camera.orthographicSize;
        public float Bottom => transform.position.y - HalfHeight;
        public float Top => transform.position.y + HalfHeight;
        public bool Creeping { get; set; }
        public int Section { get; set; }

        private float blastFrom = float.NaN;
        private float preciseY;
        private float targetY;
        private float velocity;
        private int shakePixels;
        private float shakeUntil;

        private void Awake() => Camera = GetComponent<Camera>();

        public void SnapTo(float catY, float screenFraction = -1f)
        {
            float fraction = screenFraction >= 0f ? screenFraction : StartScreenFraction;
            targetY = catY + HalfHeight * (1f - 2f * fraction);
            preciseY = targetY;
            velocity = 0f;
            Apply();
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            var cat = CatController.Instance;
            if (cat != null && cat.IsLaunching && Conductor.Instance != null)
            {
                FollowLaunch(cat, Conductor.Instance.SongBeats);
                Apply();
                return;
            }
            blastFrom = float.NaN;

            if (cat != null && cat.Settling)
            {
                Settle(cat.SettleY, dt);
                Apply();
                return;
            }

            float lead = 0f;
            if (cat != null && !cat.InIntro)
            {
                targetY = Mathf.Max(targetY, cat.transform.position.y + HalfHeight * (1f - 2f * CatScreenFraction));
                lead = Mathf.Max(0f, cat.VerticalSpeed) * SmoothTime;
            }

            if (Creeping && Conductor.Instance != null)
            {
                float perBeat = creepPerBeat[Mathf.Clamp(Section, 0, creepPerBeat.Length - 1)];
                targetY += perBeat * Conductor.Instance.Bpm / 60f * dt;
            }

            if (dt > 0f)
            {
                float next = Mathf.SmoothDamp(preciseY, targetY + lead, ref velocity, SmoothTime, Mathf.Infinity, dt);
                if (next < preciseY)
                    velocity = Mathf.Max(0f, velocity);
                preciseY = Mathf.Max(preciseY, next);
            }
            Apply();
        }

        private void Settle(float groundY, float dt)
        {
            float target = groundY + HalfHeight * (1f - 2f * StartScreenFraction);
            if (dt > 0f)
                preciseY = Mathf.SmoothDamp(preciseY, target, ref velocity, SmoothTime, Mathf.Infinity, dt);
            targetY = preciseY;
        }

        private void FollowLaunch(CatController cat, double beats)
        {
            float offset = PixelPerfectRig.Snap(HalfHeight * (1f - 2f * LaunchScreenFraction));
            if (cat.State == CatState.Blasting)
            {
                if (float.IsNaN(blastFrom))
                    blastFrom = preciseY;
                double start = cat.BlastStartBeat + BlastHoldBeats;
                float span = (float)(cat.CruiseStartBeat - start);
                float u = span > 0.01f ? Mathf.Clamp01((float)((beats - start) / span)) : 1f;
                float end = cat.CruiseStartY + offset;
                float endSlope = cat.LaunchUnitsPerBeat * span;
                float u2 = u * u;
                float u3 = u2 * u;
                preciseY = Mathf.Max(blastFrom, blastFrom * (2f * u3 - 3f * u2 + 1f) + end * (3f * u2 - 2f * u3) + endSlope * (u3 - u2));
            }
            else
            {
                preciseY = Mathf.Max(preciseY, cat.transform.position.y + offset);
            }
            targetY = preciseY;
            velocity = 0f;
        }

        public void Shake(int pixels, float seconds)
        {
            shakePixels = Mathf.Max(shakePixels, pixels);
            shakeUntil = Time.unscaledTime + seconds;
        }

        private void Apply()
        {
            var rig = PixelPerfectRig.Instance;
            Vector2 center = rig != null ? rig.CenterOffset : Vector2.zero;
            float x = center.x;
            float y = PixelPerfectRig.Snap(preciseY) + center.y;
            if (Time.unscaledTime < shakeUntil)
            {
                x += Random.Range(-shakePixels, shakePixels + 1) / (float)PixelPerfectRig.PixelsPerUnit;
                y += Random.Range(-shakePixels, shakePixels + 1) / (float)PixelPerfectRig.PixelsPerUnit;
            }
            else
            {
                shakePixels = 0;
            }
            transform.position = new Vector3(x, y, transform.position.z);
        }
    }
}
