using System;
using UnityEngine;
using UnityEngine.UI;

namespace Gluttony
{
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(Camera))]
    public class PixelPerfectRig : MonoBehaviour
    {
        public const int PixelsPerUnit = 16;
        private const int TargetWidth = 144;
        private const int MinHeight = 256;

        [SerializeField] private CanvasScaler uiScaler;
        [SerializeField] private RectTransform uiSafe;

        public static PixelPerfectRig Instance { get; private set; }
        public static event Action Resized;

        public int Scale { get; private set; } = 1;
        public int Width { get; private set; } = 144;
        public int Height { get; private set; } = 256;
        public Camera Camera { get; private set; }
        public float HalfWidthUnits => Screen.width * 0.5f / (Scale * (float)PixelsPerUnit);
        public Vector2 CenterOffset { get; private set; }

        private int screenWidth;
        private int screenHeight;
        private Rect safeArea;

        private void Awake()
        {
            Instance = this;
            Camera = GetComponent<Camera>();
            Apply();
        }

        private void Update()
        {
            if (Screen.width != screenWidth || Screen.height != screenHeight || Screen.safeArea != safeArea)
                Apply();
        }

        public static float Snap(float value) => Mathf.Round(value * PixelsPerUnit) / PixelsPerUnit;

        public Vector3 ScreenToWorld(Vector2 screen) =>
            Camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -Camera.transform.position.z));

        public float ScreenToWorldX(float screenX) => (screenX - Screen.width * 0.5f) / (Scale * (float)PixelsPerUnit);

        public Vector2 WorldToScreen(Vector3 world) => Camera.WorldToScreenPoint(world);

        public Vector2 ScreenToCanvas(Vector2 screen) =>
            new Vector2(Mathf.Floor(screen.x / Scale), Mathf.Floor(screen.y / Scale));

        public Vector2 WorldToCanvas(Vector3 world) => ScreenToCanvas(WorldToScreen(world));

        private void Apply()
        {
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            safeArea = Screen.safeArea;

            Scale = Mathf.Max(1, Mathf.Min(screenWidth / TargetWidth, screenHeight / MinHeight));
            Width = Mathf.CeilToInt(screenWidth / (float)Scale);
            Height = Mathf.CeilToInt(screenHeight / (float)Scale);
            Camera.targetTexture = null;
            Camera.orthographicSize = screenHeight / (2f * PixelsPerUnit * Scale);
            float half = 0.5f / (Scale * PixelsPerUnit);
            CenterOffset = new Vector2(screenWidth % 2 * half, screenHeight % 2 * half);

            if (uiScaler != null)
            {
                uiScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                uiScaler.scaleFactor = Scale;
                uiScaler.referencePixelsPerUnit = PixelsPerUnit;
            }

            if (uiSafe != null)
            {
                int left = Mathf.CeilToInt(safeArea.xMin / Scale);
                int bottom = Mathf.CeilToInt(safeArea.yMin / Scale);
                int right = Mathf.CeilToInt((screenWidth - safeArea.xMax) / Scale);
                int top = Mathf.CeilToInt((screenHeight - safeArea.yMax) / Scale);
                uiSafe.anchorMin = Vector2.zero;
                uiSafe.anchorMax = Vector2.one;
                uiSafe.offsetMin = new Vector2(left, bottom);
                uiSafe.offsetMax = new Vector2(-right, -top);
            }

            Resized?.Invoke();
        }
    }
}
