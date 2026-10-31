using UnityEngine;
using UnityEngine.EventSystems;

namespace Gluttony
{
    public class SongListScroll : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        private const float WheelSpeed = 240f;
        private const float MaxSpeed = 1600f;
        private const float Friction = 4f;
        private const float StopSpeed = 6f;
        private const float EdgeResistance = 0.35f;
        private const float EdgeSpring = 14f;
        private const float SpeedSmoothing = 0.35f;

        [SerializeField] private RectTransform content;

        private RectTransform viewport;
        private Canvas canvas;
        private float offset;
        private float velocity;
        private bool dragging;

        private float Scale => canvas != null ? canvas.scaleFactor : 1f;
        private float MaxOffset => Mathf.Max(0f, content.rect.height - viewport.rect.height);

        private void Awake()
        {
            viewport = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) => velocity = 0f;

        public void OnBeginDrag(PointerEventData eventData) => dragging = true;

        public void OnDrag(PointerEventData eventData)
        {
            float delta = eventData.delta.y / Scale;
            bool outside = offset < 0f || offset > MaxOffset;
            offset += outside ? delta * EdgeResistance : delta;
            float speed = delta / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            velocity = Mathf.Clamp(Mathf.Lerp(velocity, speed, SpeedSmoothing), -MaxSpeed, MaxSpeed);
        }

        public void OnEndDrag(PointerEventData eventData) => dragging = false;

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData.scrollDelta.y != 0f)
                velocity = -Mathf.Sign(eventData.scrollDelta.y) * WheelSpeed;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (!dragging)
            {
                float limit = Mathf.Clamp(offset, 0f, MaxOffset);
                if (!Mathf.Approximately(limit, offset))
                {
                    velocity = 0f;
                    offset = Mathf.Lerp(offset, limit, 1f - Mathf.Exp(-EdgeSpring * dt));
                    if (Mathf.Abs(offset - limit) < 0.5f)
                        offset = limit;
                }
                else if (Mathf.Abs(velocity) > StopSpeed)
                {
                    offset += velocity * dt;
                    velocity *= Mathf.Exp(-Friction * dt);
                }
                else
                {
                    velocity = 0f;
                }
            }
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Round(offset * Scale) / Scale);
        }
    }
}
