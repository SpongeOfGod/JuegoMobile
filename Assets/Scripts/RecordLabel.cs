using UnityEngine;

namespace Gluttony
{
    [RequireComponent(typeof(PixelLabel))]
    public class RecordLabel : MonoBehaviour
    {
        [SerializeField] private string format = "RÉCORD {0}";

        private void OnEnable() => GetComponent<PixelLabel>().Text = string.Format(format, PlayerPrefs.GetInt(ScoreManager.BestKey, 0));
    }
}
