using UnityEngine;

namespace Gluttony
{
    public class GameUI : MonoBehaviour
    {
        [SerializeField] private HudView hud;

        public void ShowHud() => hud.gameObject.SetActive(true);
    }
}
