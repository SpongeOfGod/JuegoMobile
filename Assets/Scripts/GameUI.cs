using UnityEngine;

namespace Gluttony
{
    public class GameUI : MonoBehaviour
    {
        [SerializeField] private HudView hud;
        [SerializeField] private FloatingText popupPrefab;
        [SerializeField] private RectTransform popupLayer;

        public void ShowHud() => hud.gameObject.SetActive(true);

        public FloatingText Popup(string text, Vector3 worldPosition, Color color, int scale = 1, FloatingText replace = null)
        {
            var rig = PixelPerfectRig.Instance;
            if (rig == null || popupPrefab == null)
                return null;
            if (replace != null)
                Destroy(replace.gameObject);
            var popup = Instantiate(popupPrefab, popupLayer);
            popup.Show(text, color, rig.WorldToCanvas(worldPosition), scale);
            return popup;
        }
    }
}
