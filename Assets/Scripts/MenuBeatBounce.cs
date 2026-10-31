using UnityEngine;

namespace Gluttony
{
    public class MenuBeatBounce : MonoBehaviour
    {
        [SerializeField] private int pixels = 2;

        private RectTransform rect;
        private Vector2 home;

        private void Awake()
        {
            rect = (RectTransform)transform;
            home = rect.anchoredPosition;
        }

        private void LateUpdate() => rect.anchoredPosition = home + Vector2.up * Mathf.Round(MenuMusic.Pulse * pixels);
    }
}
