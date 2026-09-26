using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gluttony
{
    public enum PopupId
    {
        Whiff,
        Early,
        Late,
        Splat,
        Combo,
        Bite,
        Feast,
        FeastPoints,
    }

    [Serializable]
    public class PopupStyle
    {
        public PopupId id;
        public string text = "";
        [Range(1, 4)] public int scale = 1;
        public Vector2 offset;
        public bool replace;
    }

    public class PopupManager : MonoBehaviour
    {
        public static PopupManager Instance { get; private set; }

        [SerializeField] private FloatingText prefab;
        [SerializeField] private string recordText = "RÉCORD {0}";
        [SerializeField] private float seconds = 1f;
        [SerializeField] private float risePixelsPerSecond = 24f;
        [SerializeField] private PopupStyle[] popups = new PopupStyle[0];

        private readonly Dictionary<PopupId, PopupStyle> styles = new Dictionary<PopupId, PopupStyle>();
        private readonly Dictionary<PopupId, FloatingText> shown = new Dictionary<PopupId, FloatingText>();

        private void Awake()
        {
            Instance = this;
            foreach (var style in popups)
                if (style != null)
                    styles[style.id] = style;
        }

        public string Record(int best) => string.Format(recordText, best);

        public void Show(PopupId id, Vector3 world, int value = 0, bool mirror = false)
        {
            var rig = PixelPerfectRig.Instance;
            if (rig == null || prefab == null || !styles.TryGetValue(id, out var style))
                return;
            if (style.replace && shown.TryGetValue(id, out var previous) && previous != null)
                Destroy(previous.gameObject);
            var offset = new Vector3(mirror ? -style.offset.x : style.offset.x, style.offset.y, 0f);
            var popup = Instantiate(prefab, transform);
            popup.Show(string.Format(style.text, value), rig.WorldToCanvas(world + offset), style.scale, seconds, risePixelsPerSecond);
            shown[id] = popup;
        }
    }
}
