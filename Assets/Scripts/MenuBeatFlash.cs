using UnityEngine;
using UnityEngine.UI;

namespace Gluttony
{
    [RequireComponent(typeof(Graphic))]
    public class MenuBeatFlash : MonoBehaviour
    {
        [SerializeField] private float rest = 0.6f;

        private Graphic graphic;
        private Color color;

        private void Awake()
        {
            graphic = GetComponent<Graphic>();
            color = graphic.color;
        }

        private void LateUpdate()
        {
            float light = Mathf.Lerp(rest, 1f, MenuMusic.Pulse);
            graphic.color = new Color(color.r * light, color.g * light, color.b * light, color.a);
        }
    }
}
