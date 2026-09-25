using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Finger = UnityEngine.InputSystem.EnhancedTouch.Finger;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Gluttony
{
    public enum Gesture
    {
        None,
        Hold,
        Swipe,
    }

    public class CatInput : MonoBehaviour
    {
        public const int MousePointerId = 1000;

        [SerializeField] private float swipeDistance = 0.04f;
        [SerializeField] private float swipeSeconds = 0.25f;

        public event Action Pressed;
        public event Action HoldReleased;
        public event Action<int> Swiped;

        public bool InputEnabled { get; private set; }
        public Gesture Current { get; private set; }
        public bool FingerDown => primaryId != -1;
        public bool Holding => Current == Gesture.Hold;
        public bool Swiping => Current == Gesture.Swipe;
        public Vector2 FingerScreen { get; private set; }
        public Vector2 AnchorScreen { get; private set; }
        public float PressedAt { get; private set; }
        public bool InSwipeWindow => Time.realtimeSinceStartup - PressedAt < swipeSeconds;

        private int primaryId = -1;
        private Vector2 primaryStart;
        private readonly HashSet<int> ignoredPointers = new HashSet<int>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            Touch.onFingerDown += OnFingerDown;
            Touch.onFingerMove += OnFingerMove;
            Touch.onFingerUp += OnFingerUp;
        }

        private void OnDisable()
        {
            Touch.onFingerDown -= OnFingerDown;
            Touch.onFingerMove -= OnFingerMove;
            Touch.onFingerUp -= OnFingerUp;
            EnhancedTouchSupport.Disable();
        }

        public void SetEnabled(bool value)
        {
            InputEnabled = value;
            primaryId = -1;
            Current = Gesture.None;
            ignoredPointers.Clear();
        }

        private void OnFingerDown(Finger finger) => PointerDown(finger.index, finger.screenPosition);
        private void OnFingerMove(Finger finger) => PointerMove(finger.index, finger.screenPosition);
        private void OnFingerUp(Finger finger) => PointerUp(finger.index, finger.screenPosition);

        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            var mouse = Mouse.current;
            if (mouse != null && Touch.activeTouches.Count == 0)
            {
                Vector2 pos = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame)
                    PointerDown(MousePointerId, pos);
                else if (mouse.leftButton.isPressed)
                    PointerMove(MousePointerId, pos);
                if (mouse.leftButton.wasReleasedThisFrame)
                    PointerUp(MousePointerId, pos);
            }
#endif
        }

        public void PointerDown(int id, Vector2 screenPos)
        {
            if (!InputEnabled)
                return;
            if (IsOverUI(screenPos))
            {
                ignoredPointers.Add(id);
                return;
            }
            if (primaryId != -1)
                return;
            primaryId = id;
            primaryStart = screenPos;
            FingerScreen = screenPos;
            AnchorScreen = screenPos;
            PressedAt = Time.realtimeSinceStartup;
            Current = Gesture.Hold;
            Pressed?.Invoke();
        }

        public void PointerMove(int id, Vector2 screenPos)
        {
            if (!InputEnabled || id != primaryId)
                return;

            FingerScreen = screenPos;
            if (Current != Gesture.Hold || !InSwipeWindow)
                return;
            int dir = SwipeDirection(screenPos, 1f);
            if (dir == 0)
                return;
            Current = Gesture.Swipe;
            Swiped?.Invoke(dir);
        }

        private int SwipeDirection(Vector2 screenPos, float scale)
        {
            Vector2 delta = screenPos - primaryStart;
            if (Mathf.Abs(delta.x) < swipeDistance * scale * Mathf.Max(1, Screen.width) || Mathf.Abs(delta.x) < Mathf.Abs(delta.y))
                return 0;
            return delta.x > 0f ? 1 : -1;
        }

        public void PointerUp(int id, Vector2 screenPos)
        {
            if (ignoredPointers.Remove(id))
                return;
            if (!InputEnabled || id != primaryId)
                return;

            var gesture = Current;
            bool quick = InSwipeWindow;
            primaryId = -1;
            Current = Gesture.None;
            if (gesture != Gesture.Hold)
                return;
            int dir = quick ? SwipeDirection(screenPos, 0.6f) : 0;
            if (dir != 0)
                Swiped?.Invoke(dir);
            else
                HoldReleased?.Invoke();
        }

        private bool IsOverUI(Vector2 screenPos)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;
            uiHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPos }, uiHits);
            return uiHits.Count > 0;
        }
    }
}
